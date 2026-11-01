// ABI 3: first argument-bearing hook. bool uses an explicit uint8_t at the C boundary.
namespace heal_hook {
using original_type = void (*)(void *, int32_t, bool, const void *);
using callback_type = int32_t (*)(void *, int32_t, uint8_t);
pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
void *original_address = nullptr, *target_address = nullptr, *stub = nullptr;
std::atomic<callback_type> callback{nullptr};
std::atomic<bool> enabled{false};
std::atomic<uint64_t> entries{0}, originals{0}, errors{0};
struct frame { original_type original; const void *method; uint64_t calls; frame *previous; };
thread_local frame *current = nullptr;
void invoke(frame &f, void *self, int32_t amount, bool broadcast) {
    f.calls++; originals.fetch_add(1, std::memory_order_relaxed); f.original(self, amount, broadcast, f.method);
}
void proxy(void *self, int32_t amount, bool broadcast, const void *method) {
    auto original = reinterpret_cast<original_type>(__atomic_load_n(&original_address, __ATOMIC_ACQUIRE));
    if (original == nullptr) __builtin_trap();
    frame f{original,method,0,current}; current = &f;
    entries.fetch_add(1,std::memory_order_relaxed);
    auto managed = callback.load(std::memory_order_acquire);
    if (enabled.load(std::memory_order_acquire) && managed != nullptr) {
        if (managed(self,amount,broadcast ? 1 : 0) != 0) {
            errors.fetch_add(1,std::memory_order_relaxed); enabled.store(false,std::memory_order_release);
            if (f.calls == 0) invoke(f,self,amount,broadcast);
        }
    } else invoke(f,self,amount,broadcast);
    current = f.previous;
}
}
IVY_EXPORT int32_t ivy_native_install_heal_hook(void *address, heal_hook::callback_type managed) {
    pthread_mutex_lock(&heal_hook::mutex);
    int32_t error = 0;
    if (address == nullptr || managed == nullptr) error = -1;
    else if (heal_hook::stub != nullptr) error = address == heal_hook::target_address ? 0 : -2;
    else {
#ifndef IVY_NATIVE_TEST
        Dl_info info{};
        if ((reinterpret_cast<uintptr_t>(address) & 3U) != 0 || dladdr(address,&info) == 0 ||
            info.dli_fname == nullptr || std::strstr(info.dli_fname,"libil2cpp.so") == nullptr) error = -4;
#endif
        if (error == 0) {
            error = shadowhook_init(SHADOWHOOK_MODE_UNIQUE,false);
            if (error == 0) {
                heal_hook::callback.store(managed,std::memory_order_release);
                heal_hook::stub = shadowhook_hook_func_addr(address,reinterpret_cast<void *>(heal_hook::proxy),&heal_hook::original_address);
                if (heal_hook::stub == nullptr) { error = shadowhook_get_errno(); if (error == 0) error = -5; }
                else if (heal_hook::original_address == nullptr) { shadowhook_unhook(heal_hook::stub); heal_hook::stub = nullptr; error = -6; }
                else { heal_hook::target_address = address; heal_hook::enabled.store(true,std::memory_order_release); }
            }
        }
    }
    pthread_mutex_unlock(&heal_hook::mutex); return error;
}
IVY_EXPORT int32_t ivy_native_call_heal_original(void *self, int32_t amount, uint8_t broadcast) {
    if (self == nullptr || heal_hook::current == nullptr) return -8;
    heal_hook::invoke(*heal_hook::current,self,amount,broadcast != 0); return 0;
}
IVY_EXPORT void ivy_native_set_heal_enabled(int32_t value) { heal_hook::enabled.store(value != 0,std::memory_order_release); }
IVY_EXPORT int64_t ivy_native_heal_stat(int32_t key) {
    switch (key) {
        case 0: return static_cast<int64_t>(heal_hook::entries.load(std::memory_order_relaxed));
        case 1: return static_cast<int64_t>(heal_hook::originals.load(std::memory_order_relaxed));
        case 2: return static_cast<int64_t>(heal_hook::errors.load(std::memory_order_relaxed));
        case 3: return heal_hook::enabled.load(std::memory_order_acquire) ? 1 : 0;
        default: return -1;
    }
}
#ifdef IVY_NATIVE_TEST
IVY_EXPORT void ivy_native_test_heal(void *self,int32_t amount,uint8_t broadcast,const void *method) { heal_hook::proxy(self,amount,broadcast != 0,method); }
#endif
