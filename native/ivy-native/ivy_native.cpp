#include <atomic>
#include <cstdint>
#include <cstring>
#include <dlfcn.h>
#include <pthread.h>
#include <sys/syscall.h>
#include <unistd.h>
#include "hook_backend.h"

#define IVY_EXPORT extern "C" __attribute__((visibility("default")))

using world_update = void (*)(void *, const void *);
using world_callback = int32_t (*)(void *, const void *, uint64_t, int32_t);

namespace {
pthread_mutex_t control = PTHREAD_MUTEX_INITIALIZER;
void *original_address = nullptr;
void *target_address = nullptr;
void *hook_stub = nullptr;
bool retired = false;
std::atomic<world_callback> callback{nullptr};
std::atomic<bool> enabled{false};
std::atomic<bool> installed{false};
std::atomic<uint64_t> ticks{0}, originals{0}, callbacks{0}, failures{0}, in_flight{0}, installs{0};
std::atomic<int32_t> last_error{0}, first_thread{0};

struct call_frame {
    world_update original;
    const void *method;
    uint64_t original_calls;
    call_frame *previous;
};
thread_local call_frame *current_frame = nullptr;

void call_original(call_frame &frame, void *instance) {
    frame.original_calls++;
    originals.fetch_add(1, std::memory_order_relaxed);
    frame.original(instance, frame.method);
}

void world_proxy(void *instance, const void *method_info) {
    auto original = reinterpret_cast<world_update>(__atomic_load_n(&original_address, __ATOMIC_ACQUIRE));
    if (original == nullptr) __builtin_trap();
    call_frame frame{original, method_info, 0, current_frame};
    current_frame = &frame;
    const auto tick = ticks.fetch_add(1, std::memory_order_relaxed) + 1;
    in_flight.fetch_add(1, std::memory_order_acq_rel);
    const auto managed = callback.load(std::memory_order_acquire);
    if (enabled.load(std::memory_order_acquire) && managed != nullptr) {
        const int32_t tid = static_cast<int32_t>(syscall(SYS_gettid));
        int32_t expected = 0;
        first_thread.compare_exchange_strong(expected, tid, std::memory_order_relaxed);
        callbacks.fetch_add(1, std::memory_order_relaxed);
        if (managed(instance, method_info, tick, tid) != 0) {
            failures.fetch_add(1, std::memory_order_relaxed);
            enabled.store(false, std::memory_order_release);
            // Failure before orig: preserve the game. Never repeat an original already invoked.
            if (frame.original_calls == 0) call_original(frame, instance);
        }
    } else {
        call_original(frame, instance);
    }
    in_flight.fetch_sub(1, std::memory_order_acq_rel);
    current_frame = frame.previous;
}

int32_t finish(int32_t result) {
    last_error.store(result, std::memory_order_relaxed);
    pthread_mutex_unlock(&control);
    return result;
}
}

IVY_EXPORT uint32_t ivy_native_abi_version() { return 3; }

IVY_EXPORT int32_t ivy_native_call_world_original(void *instance) {
    if (instance == nullptr || current_frame == nullptr) return -8;
    call_original(*current_frame, instance);
    return 0;
}

IVY_EXPORT int32_t ivy_native_install_world_hook(void *address, world_callback managed) {
    pthread_mutex_lock(&control);
    if (address == nullptr || managed == nullptr) return finish(-1);
    if (hook_stub != nullptr) return finish(address == target_address ? 0 : -2);
    if (retired) return finish(-3);
#ifndef IVY_NATIVE_TEST
    Dl_info info{};
    if ((reinterpret_cast<uintptr_t>(address) & 3U) != 0 || dladdr(address, &info) == 0 ||
        info.dli_fname == nullptr || std::strstr(info.dli_fname, "libil2cpp.so") == nullptr)
        return finish(-4);
#endif
    const int init_result = shadowhook_init(SHADOWHOOK_MODE_UNIQUE, false);
    if (init_result != 0) return finish(init_result);
    callback.store(managed, std::memory_order_release);
    enabled.store(true, std::memory_order_release);
    hook_stub = shadowhook_hook_func_addr(address, reinterpret_cast<void *>(world_proxy), &original_address);
    if (hook_stub == nullptr) {
        enabled.store(false, std::memory_order_release);
        const int error = shadowhook_get_errno();
        return finish(error == 0 ? -5 : error);
    }
    if (original_address == nullptr) {
        enabled.store(false, std::memory_order_release);
        shadowhook_unhook(hook_stub);
        hook_stub = nullptr;
        retired = true;
        return finish(-6);
    }
    target_address = address;
    installed.store(true, std::memory_order_release);
    installs.fetch_add(1, std::memory_order_relaxed);
    return finish(0);
}

IVY_EXPORT int32_t ivy_native_set_callbacks_enabled(int32_t value) {
    pthread_mutex_lock(&control);
    if (!installed.load(std::memory_order_acquire)) return finish(-7);
    enabled.store(value != 0, std::memory_order_release);
    return finish(0);
}

IVY_EXPORT int32_t ivy_native_uninstall_world_hook() {
    pthread_mutex_lock(&control);
    enabled.store(false, std::memory_order_release);
    if (hook_stub == nullptr) return finish(0);
    const int result = shadowhook_unhook(hook_stub);
    if (result != 0) return finish(result);
    hook_stub = nullptr;
    retired = true;
    installed.store(false, std::memory_order_release);
    return finish(0);
}

IVY_EXPORT int64_t ivy_native_get_stat(int32_t key) {
    switch (key) {
        case 0: return static_cast<int64_t>(ticks.load(std::memory_order_relaxed));
        case 1: return static_cast<int64_t>(originals.load(std::memory_order_relaxed));
        case 2: return static_cast<int64_t>(callbacks.load(std::memory_order_relaxed));
        case 3: return enabled.load(std::memory_order_acquire) ? 1 : 0;
        case 4: return installed.load(std::memory_order_acquire) ? 1 : 0;
        case 5: return last_error.load(std::memory_order_relaxed);
        case 6: return first_thread.load(std::memory_order_relaxed);
        case 7: return static_cast<int64_t>(in_flight.load(std::memory_order_acquire));
        case 8: return static_cast<int64_t>(failures.load(std::memory_order_relaxed));
        case 9: return static_cast<int64_t>(installs.load(std::memory_order_relaxed));
        default: return -1;
    }
}

IVY_EXPORT const char *ivy_native_error_message(int32_t error) {
    switch (error) {
        case 0: return "OK";
        case -1: return "Null target or callback";
        case -2: return "Another world update target is already installed";
        case -3: return "Hook removed; restart process before installing again";
        case -4: return "Target is not an aligned libil2cpp function";
        case -5: return "Hook backend returned no stub";
        case -6: return "Hook backend returned no original function";
        case -7: return "World update hook is not installed";
        case -8: return "Original is only callable inside its matching hook on the same thread";
        default: return shadowhook_to_errmsg(error);
    }
}

#ifdef IVY_NATIVE_TEST
IVY_EXPORT void ivy_native_test_tick(void *instance, const void *method_info) { world_proxy(instance, method_info); }
#endif

#include "heal_hook.h"

#include "runtime_ready.h"

#include "return_hooks.g.inc"
