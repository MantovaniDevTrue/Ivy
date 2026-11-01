#include <cassert>
#include <cstdint>
#include <iostream>
#include "hook_backend.h"

using callback_type = int32_t (*)(void *, uint64_t, uint64_t, int32_t);
extern "C" int32_t ivy_native_install_world_hook(void *, callback_type);
extern "C" int32_t ivy_native_uninstall_world_hook();
extern "C" int32_t ivy_native_set_callbacks_enabled(int32_t);
extern "C" int64_t ivy_native_get_stat(int32_t);
extern "C" void ivy_native_test_tick(void *, const void *);

static int stage = 0, original_count = 0, managed_count = 0;
static bool fail_callback = false;
static void original(void *self, const void *method) {
    assert(self == reinterpret_cast<void *>(1));
    assert(method == reinterpret_cast<void *>(2));
    stage = 1;
    original_count++;
}
static int32_t managed(void *self, uint64_t tick, uint64_t calls, int32_t tid) {
    assert(self == reinterpret_cast<void *>(1));
    assert(stage == 1 && static_cast<int>(calls) == original_count);
    assert(tick == calls && tid > 0);
    stage = 2;
    managed_count++;
    return fail_callback ? -1 : 0;
}

extern "C" int shadowhook_init(shadowhook_mode_t mode, bool debug) { assert(mode == SHADOWHOOK_MODE_UNIQUE && !debug); return 0; }
extern "C" void *shadowhook_hook_func_addr(void *target, void *, void **previous) {
    *previous = target;
    return reinterpret_cast<void *>(3);
}
extern "C" int shadowhook_unhook(void *stub) { assert(stub == reinterpret_cast<void *>(3)); return 0; }
extern "C" int shadowhook_get_errno() { return 0; }
extern "C" const char *shadowhook_to_errmsg(int) { return "mock"; }

int main() {
    const auto address = reinterpret_cast<void *>(original);
    assert(ivy_native_install_world_hook(nullptr, managed) == -1);
    assert(ivy_native_install_world_hook(address, nullptr) == -1);
    assert(ivy_native_install_world_hook(address, managed) == 0);
    assert(ivy_native_install_world_hook(address, managed) == 0);
    assert(ivy_native_get_stat(9) == 1);
    assert(ivy_native_install_world_hook(reinterpret_cast<void *>(4), managed) == -2);
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(original_count == 1 && managed_count == 1 && stage == 2);
    assert(ivy_native_get_stat(0) == 1 && ivy_native_get_stat(1) == 1 && ivy_native_get_stat(2) == 1);
    assert(ivy_native_set_callbacks_enabled(0) == 0);
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(original_count == 2 && managed_count == 1);
    assert(ivy_native_set_callbacks_enabled(1) == 0);
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(original_count == 3 && managed_count == 2);
    fail_callback = true;
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(ivy_native_get_stat(8) == 1 && ivy_native_get_stat(3) == 0 && ivy_native_get_stat(7) == 0);
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(original_count == 5 && managed_count == 3);
    assert(ivy_native_uninstall_world_hook() == 0 && ivy_native_get_stat(4) == 0);
    ivy_native_test_tick(reinterpret_cast<void *>(1), reinterpret_cast<void *>(2));
    assert(original_count == 6 && managed_count == 3);
    assert(ivy_native_uninstall_world_hook() == 0);
    assert(ivy_native_install_world_hook(address, managed) == -3);
    std::cout << "Native bridge control tests passed (mock hook backend; no ARM64 patch executed).\n";
}
