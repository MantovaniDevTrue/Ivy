// Arm before Unity loads libil2cpp. Library mapping is not proof of VM readiness.
namespace runtime_ready {
pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
void *original_init = nullptr, *original_utf16 = nullptr;
void *stub_init = nullptr, *stub_utf16 = nullptr;
std::atomic<bool> ready{false};
std::atomic<void *> domain{nullptr};
std::atomic<uint64_t> returns{0};
using init_function = bool (*)(const void *);
bool after_init(void *const *original, const void *name) {
    auto fn = reinterpret_cast<init_function>(__atomic_load_n(original,__ATOMIC_ACQUIRE));
    if (fn == nullptr) __builtin_trap();
    const bool ok = fn(name);
    returns.fetch_add(1,std::memory_order_relaxed);
    if (ok) {
        // Resolve the domain on Unity's already-attached initialization thread.
        // domain_get may allocate its managed wrapper; never do that from an unattached Mono worker.
        void *handle = shadowhook_dlopen("libil2cpp.so");
        if (handle != nullptr) {
            auto get_domain = reinterpret_cast<void *(*)()>(shadowhook_dlsym(handle,"il2cpp_domain_get"));
            if (get_domain != nullptr) {
                void *value = get_domain();
                if (value != nullptr) { domain.store(value,std::memory_order_release); ready.store(true,std::memory_order_release); }
            }
            shadowhook_dlclose(handle);
        }
    }
    return ok;
}
bool init_proxy(const void *name) { return after_init(&original_init,name); }
bool utf16_proxy(const void *name) { return after_init(&original_utf16,name); }
}
IVY_EXPORT int32_t ivy_native_watch_runtime() {
    pthread_mutex_lock(&runtime_ready::mutex);
    int result = shadowhook_init(SHADOWHOOK_MODE_UNIQUE,false);
    if (result == 0) {
        if (runtime_ready::stub_init == nullptr) runtime_ready::stub_init = shadowhook_hook_sym_name("libil2cpp.so","il2cpp_init",reinterpret_cast<void *>(runtime_ready::init_proxy),&runtime_ready::original_init);
        if (runtime_ready::stub_utf16 == nullptr) runtime_ready::stub_utf16 = shadowhook_hook_sym_name("libil2cpp.so","il2cpp_init_utf16",reinterpret_cast<void *>(runtime_ready::utf16_proxy),&runtime_ready::original_utf16);
        if (runtime_ready::stub_init == nullptr || runtime_ready::stub_utf16 == nullptr) { result = shadowhook_get_errno(); if (result == 0) result = -5; }
    }
    pthread_mutex_unlock(&runtime_ready::mutex); return result;
}
IVY_EXPORT int32_t ivy_native_runtime_ready() { return runtime_ready::ready.load(std::memory_order_acquire) ? 1 : 0; }
IVY_EXPORT void *ivy_native_runtime_domain() { return runtime_ready::ready.load(std::memory_order_acquire) ? runtime_ready::domain.load(std::memory_order_acquire) : nullptr; }
IVY_EXPORT uint64_t ivy_native_runtime_init_returns() { return runtime_ready::returns.load(std::memory_order_acquire); }
#ifdef IVY_NATIVE_TEST
IVY_EXPORT bool ivy_native_test_init(const void *name, bool utf16) { return utf16 ? runtime_ready::utf16_proxy(name) : runtime_ready::init_proxy(name); }
#endif
