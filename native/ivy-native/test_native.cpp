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
static void test_heal();
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
    test_heal();
    std::cout << "Native world control tests passed: zero/one/two originals, error fallback without duplicates, removal (mock backend).\n";
}

using heal_callback_type = int32_t (*)(void *,int32_t,uint8_t);
extern "C" int32_t ivy_native_install_heal_hook(void*,heal_callback_type);
extern "C" int32_t ivy_native_call_heal_original(void*,int32_t,uint8_t);
extern "C" void ivy_native_set_heal_enabled(int32_t);
extern "C" int64_t ivy_native_heal_stat(int32_t);
extern "C" void ivy_native_test_heal(void*,int32_t,uint8_t,const void*);
static int heal_originals=0,heal_mode=0,last_amount=0;static bool last_broadcast=false;
static void heal_original(void *p,int32_t amount,bool broadcast,const void *method){assert(p==self&&method==reinterpret_cast<void*>(2));heal_originals++;last_amount=amount;last_broadcast=broadcast;}
static int32_t heal_managed(void *p,int32_t amount,uint8_t broadcast){
 assert(p==self&&amount==5&&broadcast==1);
 if(heal_mode==1)return 0;
 if(heal_mode==3)return -1;
 assert(ivy_native_call_heal_original(p,amount+7,0)==0);
 if(heal_mode==2)assert(ivy_native_call_heal_original(p,-9,1)==0);
 if(heal_mode==5){heal_mode=1;ivy_native_test_heal(p,amount,broadcast,reinterpret_cast<void*>(2));assert(ivy_native_call_heal_original(p,99,0)==0);}
 return heal_mode==4?-1:0;
}
static void test_heal(){
 auto target=reinterpret_cast<void*>(heal_original);
 assert(ivy_native_call_heal_original(self,1,1)==-8);
 assert(ivy_native_install_heal_hook(nullptr,heal_managed)==-1);
 assert(ivy_native_install_heal_hook(target,heal_managed)==0);
 assert(ivy_native_install_heal_hook(target,heal_managed)==0);
 auto tick=[](){ivy_native_test_heal(self,5,1,reinterpret_cast<void*>(2));};
 tick();assert(heal_originals==1&&last_amount==12&&!last_broadcast);
 heal_mode=1;tick();assert(heal_originals==1);
 heal_mode=2;tick();assert(heal_originals==3&&last_amount==-9&&last_broadcast);
 heal_mode=3;tick();assert(heal_originals==4&&last_amount==5&&last_broadcast&&ivy_native_heal_stat(3)==0);
 ivy_native_set_heal_enabled(1);heal_mode=4;tick();assert(heal_originals==5&&ivy_native_heal_stat(3)==0);
 ivy_native_set_heal_enabled(1);heal_mode=5;tick();assert(heal_originals==7&&last_amount==99);
 ivy_native_set_heal_enabled(0);tick();assert(heal_originals==8);
 assert(ivy_native_heal_stat(2)==2&&ivy_native_call_heal_original(self,1,1)==-8);
 std::cout<<"Native ABI 3 heal tests passed: int/bool changes, zero/two originals, recursion, fallback and disable.\n";
}
