// Deterministic fake IL2CPP C exports: exercises the real C# marshaller on Linux.
#include <cstdint>
#include <cstdlib>
#include <cstring>
struct Type {const char *name; int kind;};
Type voidType{"System.Void",1}, boolType{"System.Boolean",2}, charType{"System.Char",3}, i1{"System.SByte",4}, u1{"System.Byte",5}, i2{"System.Int16",6}, u2{"System.UInt16",7}, i4{"System.Int32",8}, u4{"System.UInt32",9}, i8{"System.Int64",10}, u8{"System.UInt64",11}, r4{"System.Single",12}, r8{"System.Double",13}, playerType{"Terraria.Player",18},itemType{"Terraria.Item",18};
struct Class{const char *name;};Class playerClass{"Player"}, itemClass{"Item"}, entityClass{"Entity"}, exceptionClass{"ProbeException"};
struct Object{Class *klass; uint64_t fields[20];};Object player{&playerClass,{}},item{&itemClass,{}},exception{&exceptionClass,{}};
struct Field{const char *name;Type *type;uint32_t flags;int index;};
Field fields[]={{"statMana",&i4,0,0},{"dead",&boolType,0,1},{"F1",&i1,0,2},{"F2",&u1,0,3},{"F3",&i2,0,4},{"F4",&u2,0,5},{"F5",&charType,0,6},{"F6",&u4,0,7},{"F7",&i8,0,8},{"F8",&u8,0,9},{"F9",&r4,0,10},{"F10",&r8,0,11},{"global",&i4,0x10,12},{"readonly",&i4,0x20,13},{"reference",&playerType,0x10,14}};
uint64_t globals[20]{};int lookups=0,invocations=0;uint64_t box=0;
struct Method{void *address;const char *name;Type *result;Type *args[2];uint32_t count,flags;int mode;};
void dummy(){}
Method methods[]={
 {reinterpret_cast<void*>(dummy),"Overload",&i4,{&i4,&i4},2,0,0},
 {reinterpret_cast<void*>(dummy),"Overload",&i4,{&i4,&boolType},2,0,1},
 {reinterpret_cast<void*>(dummy),"Throw",&voidType,{},0,0,2},
 {reinterpret_cast<void*>(dummy),"GetPlayer",&playerType,{},0,0x10,3},
 {reinterpret_cast<void*>(dummy),"AcceptPlayer",&boolType,{&playerType},1,0x10,4},
 {reinterpret_cast<void*>(dummy),"EmptyResult",&i4,{},0,0,5},
 {reinterpret_cast<void*>(dummy),"HealEffect",&voidType,{&i4,&boolType},2,0,6},
 {reinterpret_cast<void*>(dummy),"IsZero",&boolType,{&i4},1,0,7}
};
extern "C" {
void *il2cpp_domain_get(){return &player;}
void **il2cpp_domain_get_assemblies(void*,size_t *count){static void *a[]={&player};*count=1;return a;}
void *il2cpp_assembly_get_image(void *a){return a;}
const char *il2cpp_image_get_name(void*){return "Assembly-CSharp.dll";}
Class *il2cpp_class_from_name(void*,const char *ns,const char *name){if(std::strcmp(ns,"Terraria"))return nullptr;if(!std::strcmp(name,"Player"))return &playerClass;if(!std::strcmp(name,"Entity"))return &entityClass;if(!std::strcmp(name,"Item"))return &itemClass;return nullptr;}
Method *il2cpp_class_get_methods(void*,void **iter){lookups++;auto i=reinterpret_cast<uintptr_t>(*iter);if(i>=sizeof(methods)/sizeof(Method))return nullptr;*iter=reinterpret_cast<void*>(i+1);return methods+i;}
const char *il2cpp_method_get_name(Method *m){return m->name;}
Type *il2cpp_method_get_return_type(Method *m){return m->result;}
Type *il2cpp_method_get_param(Method *m,uint32_t i){return m->args[i];}
uint32_t il2cpp_method_get_param_count(Method *m){return m->count;}
uint32_t il2cpp_method_get_flags(Method *m,uint32_t *impl){*impl=0;return m->flags;}
uint8_t il2cpp_method_is_generic(Method*){return 0;}
uint8_t il2cpp_type_is_byref(Type*){return 0;}
char *il2cpp_type_get_name(Type *t){return strdup(t->name);}
void il2cpp_free(void *p){free(p);}
int il2cpp_type_get_type(Type *t){return t->kind;}
Class *il2cpp_class_from_type(Type *t){return t==&playerType?&playerClass:t==&itemType?&itemClass:nullptr;}
Field *il2cpp_class_get_field_from_name(Class*,const char *name){lookups++;for(auto &f:fields)if(!std::strcmp(f.name,name))return &f;return nullptr;}
Type *il2cpp_field_get_type(Field *f){return f->type;}
uint32_t il2cpp_field_get_flags(Field *f){return f->flags;}
int size(Type *t){int k=t->kind;return k==2||k==4||k==5?1:k==3||k==6||k==7?2:k==8||k==9||k==12?4:8;}
void il2cpp_field_get_value(Object *o,Field *f,void *v){memcpy(v,&o->fields[f->index],size(f->type));}
void il2cpp_field_set_value(Object *o,Field *f,void *v){memcpy(&o->fields[f->index],v,size(f->type));}
void il2cpp_field_static_get_value(Field *f,void *v){memcpy(v,&globals[f->index],size(f->type));}
void il2cpp_field_static_set_value(Field *f,void *v){memcpy(&globals[f->index],v,size(f->type));}
void *il2cpp_runtime_invoke(Method *m,void*,void **args,void **ex){
 invocations++;*ex=nullptr;
 switch(m->mode){
 case 0: box=static_cast<uint32_t>(*static_cast<int32_t*>(args[0])+*static_cast<int32_t*>(args[1]));return &box;
 case 1: box=static_cast<uint32_t>(*static_cast<int32_t*>(args[0])+(*static_cast<uint8_t*>(args[1])?100:0));return &box;
 case 2: *ex=&exception;return nullptr;
 case 3: return &player;
 case 4: box=args[0]==&player?1:0;return &box;
 case 5: return nullptr;
 case 7: box=*static_cast<int32_t*>(args[0])==0?1:0;return &box;
 default: return nullptr;
 }
}
void *il2cpp_object_unbox(void *o){return o;}
Class *il2cpp_object_get_class(Object *o){return o->klass;}
uint8_t il2cpp_class_is_assignable_from(Class *parent,Class *child){return parent==child||(parent==&entityClass&&(child==&playerClass||child==&itemClass));}
void il2cpp_runtime_class_init(Class*){}
const char *il2cpp_class_get_name(Class *c){return c->name;}
void *ivy_test_player(){return &player;} void *ivy_test_item(){return &item;}int ivy_test_lookups(){return lookups;}int ivy_test_invocations(){return invocations;}
}
