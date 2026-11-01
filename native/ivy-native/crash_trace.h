// Diagnostic register snapshot. No unwinding, allocation or managed code in a signal handler.
#include <cerrno>
#include <csignal>
#include <cstdio>
#include <fcntl.h>
#include <ucontext.h>

namespace crash_trace {
constexpr int signals[]={SIGSEGV,SIGBUS,SIGABRT};
struct sigaction previous[3]{};
int output=-1;
off_t slots_base=-1;
std::atomic<uint32_t> observations{0};
static_assert(std::atomic<uint32_t>::is_always_lock_free);
pthread_mutex_t lock=PTHREAD_MUTEX_INITIALIZER;
void append(const char *data,size_t size){
    while(size>0){auto written=write(output,data,size);if(written<0&&errno==EINTR)continue;if(written<=0)break;data+=written;size-=static_cast<size_t>(written);}
}
struct line {
    char data[1024]{};size_t size=0;
    void text(const char *s){while(*s&&size<sizeof(data))data[size++]=*s++;}
    void hex(uintptr_t n){text("0x");bool started=false;for(int shift=static_cast<int>(sizeof(n)*8)-4;shift>=0;shift-=4){auto digit=(n>>shift)&15;if(digit||started||shift==0){if(size<sizeof(data))data[size++]="0123456789abcdef"[digit];started=true;}}}
};
void observe(int signal,siginfo_t *info,void *context){
    const int saved_errno=errno;
    if(output>=0&&slots_base>=0){
        auto sequence=observations.fetch_add(1,std::memory_order_relaxed);
        line message;message.text("\nSIGNAL OBSERVED (may be handled by original runtime): signal=");message.hex(static_cast<uintptr_t>(signal));
        message.text(" sequence=");message.hex(sequence);
        message.text(" tid=");message.hex(static_cast<uintptr_t>(syscall(SYS_gettid)));
        message.text(" code=");message.hex(info?static_cast<uintptr_t>(info->si_code):0);
        message.text(" fault=");message.hex(info?reinterpret_cast<uintptr_t>(info->si_addr):0);
        auto *state=static_cast<ucontext_t*>(context);
        if(state){
#if defined(__aarch64__)
            message.text(" pc=");message.hex(state->uc_mcontext.pc);
            message.text(" lr=");message.hex(state->uc_mcontext.regs[30]);
            message.text(" sp=");message.hex(state->uc_mcontext.sp);
            message.text(" fp=");message.hex(state->uc_mcontext.regs[29]);
            for(int i=0;i<8;i++){message.text(" x");message.hex(static_cast<uintptr_t>(i));message.text("=");message.hex(state->uc_mcontext.regs[i]);}
#elif defined(__x86_64__)
            message.text(" pc=");message.hex(state->uc_mcontext.gregs[REG_RIP]);
            message.text(" sp=");message.hex(state->uc_mcontext.gregs[REG_RSP]);
#endif
        }
        message.text("\n");
        for(size_t i=message.size;i<sizeof(message.data);i++)message.data[i]=' ';
        message.data[sizeof(message.data)-1]='\n';
        const auto position=slots_base+static_cast<off_t>(sequence%32)*sizeof(message.data);
        ssize_t written;do{written=pwrite(output,message.data,sizeof(message.data),position);}while(written<0&&errno==EINTR);
    }
    errno=saved_errno;
    for(size_t i=0;i<3;i++)if(signals[i]==signal){
        auto &action=previous[i];
        if(action.sa_handler==SIG_IGN)return;
        if(action.sa_handler==SIG_DFL){sigaction(signal,&action,nullptr);raise(signal);return;}
        if(action.sa_flags&SA_SIGINFO)action.sa_sigaction(signal,info,context);else action.sa_handler(signal);
        return;
    }
}
}
IVY_EXPORT int32_t ivy_crash_trace_configure(const char *path){
    if(path==nullptr||path[0]!='/')return -1;
    pthread_mutex_lock(&crash_trace::lock);
    if(crash_trace::output<0){
        // One file per process attempt, with one previous attempt retained.
        char old[4096];int count=snprintf(old,sizeof(old),"%s.previous",path);
        if(count<=0||static_cast<size_t>(count)>=sizeof(old)){pthread_mutex_unlock(&crash_trace::lock);return -2;}
        rename(path,old);
        crash_trace::output=open(path,O_WRONLY|O_CREAT|O_TRUNC|O_CLOEXEC,0600);
    }
    int result=crash_trace::output<0?-3:0;
    pthread_mutex_unlock(&crash_trace::lock);return result;
}
IVY_EXPORT int32_t ivy_crash_trace_arm(){
    using namespace crash_trace;
    if(output<0)return -1;
    pthread_mutex_lock(&lock);
    lseek(output,0,SEEK_END);
    const char header[]="\nNative signal observer armed; original runtime handlers preserved. Addresses are absolute; mappings follow.\n";
    append(header,sizeof(header)-1);
    int maps=open("/proc/self/maps",O_RDONLY|O_CLOEXEC);
    if(maps>=0){char buffer[4096];size_t total=0;ssize_t count;
        while(total<262144&&(count=read(maps,buffer,sizeof(buffer)))>0){append(buffer,static_cast<size_t>(count));total+=static_cast<size_t>(count);}close(maps);}
    slots_base=lseek(output,0,SEEK_END);
    int result=slots_base<0?-4:0;
    for(size_t i=0;i<3;i++){
        struct sigaction action{};
        if(sigaction(signals[i],nullptr,&action)!=0){result=-2;continue;}
        if((action.sa_flags&SA_SIGINFO)&&action.sa_sigaction==observe)continue;
        previous[i]=action;action.sa_sigaction=observe;action.sa_flags|=SA_SIGINFO;
        if(sigaction(signals[i],&action,nullptr)!=0)result=-3;
    }
    pthread_mutex_unlock(&lock);return result;
}
