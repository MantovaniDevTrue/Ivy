#include <cassert>
#include <cstdint>
#include <iostream>
#include "hook_backend.h"
using callback_type = int32_t (*)(void *, const void *, uint64_t, int32_t);
extern "C" int32_t ivy_native_install_world_hook(void *, callback_type);
extern "C" int32_t ivy_native_uninstall_world_hook();
extern "C" int32_t ivy_native_set_callbacks_enabled(int32_t);
extern "C" int32_t ivy_native_call_world_original(void *);
extern "C" int64_t ivy_native_get_stat(int32_t);
extern "C" void ivy_native_test_tick(void *, const void *);
static int originals = 0, callbacks = 0, mode = 0;
static void *self = reinterpret_cast<void *>(1);
static void original(void *instance, const void *method) {
    assert(instance == self && method == reinterpret_cast<void *>(2)); originals++;
}
static int32_t managed(void *instance, const void *method, uint64_t tick, int32_t tid) {
    assert(instance == self && method == reinterpret_cast<void *>(2) && tick > 0 && tid > 0);
    callbacks++;
    if (mode == 1) return 0; // Deliberately omit orig.
    if (mode == 3) return -1; // Fail before orig.
    const int before = originals;
    assert(ivy_native_call_world_original(instance) == 0 && originals == before + 1);
    if (mode == 2) assert(ivy_native_call_world_original(instance) == 0 && originals == before + 2);
    return mode == 4 ? -1 : 0;
}
extern "C" int shadowhook_init(shadowhook_mode_t m, bool debug) { assert(m == SHADOWHOOK_MODE_UNIQUE && !debug); return 0; }
extern "C" void *shadowhook_hook_func_addr(void *target, void *, void **previous) { *previous = target; return reinterpret_cast<void *>(3); }
extern "C" int shadowhook_unhook(void *stub) { assert(stub == reinterpret_cast<void *>(3)); return 0; }
extern "C" int shadowhook_get_errno() { return 0; }
extern "C" const char *shadowhook_to_errmsg(int) { return "mock"; }
static void tick() { ivy_native_test_tick(self, reinterpret_cast<void *>(2)); }
int main() {
    auto address = reinterpret_cast<void *>(original);
    assert(ivy_native_call_world_original(self) == -8);
    assert(ivy_native_install_world_hook(nullptr, managed) == -1);
    assert(ivy_native_install_world_hook(address, nullptr) == -1);
    assert(ivy_native_install_world_hook(address, managed) == 0);
    assert(ivy_native_install_world_hook(address, managed) == 0 && ivy_native_get_stat(9) == 1);
    assert(ivy_native_install_world_hook(reinterpret_cast<void *>(4), managed) == -2);
    tick(); assert(originals == 1 && callbacks == 1);
    mode = 1; tick(); assert(originals == 1 && callbacks == 2);
    mode = 2; tick(); assert(originals == 3 && callbacks == 3);
    assert(ivy_native_set_callbacks_enabled(0) == 0);
    tick(); assert(originals == 4 && callbacks == 3);
    assert(ivy_native_set_callbacks_enabled(1) == 0);
    mode = 3; tick(); assert(originals == 5 && callbacks == 4 && ivy_native_get_stat(3) == 0);
    tick(); assert(originals == 6 && callbacks == 4);
    assert(ivy_native_set_callbacks_enabled(1) == 0);
    mode = 4; tick(); assert(originals == 7 && callbacks == 5 && ivy_native_get_stat(3) == 0);
    assert(ivy_native_get_stat(8) == 2 && ivy_native_get_stat(7) == 0);
    assert(ivy_native_call_world_original(self) == -8);
    assert(ivy_native_uninstall_world_hook() == 0 && ivy_native_get_stat(4) == 0);
    tick(); assert(originals == 8 && callbacks == 5);
    assert(ivy_native_uninstall_world_hook() == 0);
    assert(ivy_native_install_world_hook(address, managed) == -3);
    std::cout << "Native ABI 2 control tests passed: zero/one/two originals, error fallback without duplicates, removal (mock backend).\n";
}
