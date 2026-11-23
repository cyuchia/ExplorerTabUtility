#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <ole2.h>
#include <exdisp.h>
#include <ocidl.h>
#include <shlobj.h>
#include <commctrl.h>
#include <dwmapi.h>
#include <new>
#include <cstdio>

// Per-window COM extension. It never hooks other processes, reads user files,
// or substitutes explorer.exe. With no live ETU controller it is inert.
static const CLSID CLSID_Bridge = {0x54bc92a2,0xd2b5,0x4bc6,{0x8f,0x9b,0x5c,0x64,0xf6,0x14,0xa3,0x09}};
#ifdef ETU_BRIDGE_TEST
static constexpr wchar_t ControllerTitle[] = L"ETU.NativeVisibility.IsolatedTest.Controller";
#else
static constexpr wchar_t ControllerTitle[] = L"ETU.NativeVisibility.Controller.v2";
#endif
static constexpr wchar_t GuardProp[] = L"ETU.NativeVisibility.Guarded.v1";
static constexpr wchar_t DoneProp[] = L"ETU.NativeVisibility.Transferred.v1";
static constexpr wchar_t ReleaseName[] = L"ETU.NativeVisibility.Release.v1";
static LONG objects = 0;
static HMODULE module = nullptr;

static void Log(const wchar_t* event, HWND hwnd, int detail = 0) {
#ifdef ETU_BRIDGE_TEST
    (void)event; (void)hwnd; (void)detail;
    return;
#else
    wchar_t dir[MAX_PATH], file[MAX_PATH];
    if (!GetEnvironmentVariableW(L"LOCALAPPDATA", dir, MAX_PATH)) return;
    wcscat_s(dir, L"\\ExplorerTabUtilityBridge");
    CreateDirectoryW(dir, nullptr);
    swprintf_s(file, L"%s\\native-%lu.log", dir, GetCurrentProcessId());
    HANDLE h = CreateFileW(file, FILE_APPEND_DATA, FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,
        nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;
    // A single session has a bounded log. No paths, selection names or content.
    LARGE_INTEGER size{}; GetFileSizeEx(h, &size);
    if (size.QuadPart < 2*1024*1024) {
        SYSTEMTIME t; GetLocalTime(&t);
        char line[256];
        int n = sprintf_s(line, "%04u-%02u-%02uT%02u:%02u:%02u.%03u tid=%lu hwnd=%p %ls detail=%d visible=%d\r\n",
            t.wYear,t.wMonth,t.wDay,t.wHour,t.wMinute,t.wSecond,t.wMilliseconds,
            GetCurrentThreadId(),hwnd,event,detail,IsWindowVisible(hwnd));
        DWORD done; WriteFile(h,line,n,&done,nullptr);
    }
    CloseHandle(h);
#endif
}

static bool IsExplorer() {
    wchar_t exe[MAX_PATH], expected[MAX_PATH];
    if (!GetModuleFileNameW(nullptr,exe,MAX_PATH) || !GetWindowsDirectoryW(expected,MAX_PATH)) return false;
    wcscat_s(expected,L"\\explorer.exe");
    return _wcsicmp(exe,expected)==0;
}

static HWND Controller() {
    HWND h = FindWindowExW(HWND_MESSAGE,nullptr,nullptr,ControllerTitle);
    if (!h) return nullptr;
    DWORD pid=0; GetWindowThreadProcessId(h,&pid);
    HANDLE p=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);
    if (!p) return nullptr;
    wchar_t path[MAX_PATH]{}; DWORD n=MAX_PATH;
    bool ok=QueryFullProcessImageNameW(p,0,path,&n)!=FALSE;
    CloseHandle(p);
    const wchar_t* name=wcsrchr(path,L'\\');
    return ok && name && _wcsicmp(name+1,L"ExplorerTabUtility.exe")==0 ? h : nullptr;
}

struct WindowGuard {
    HWND hwnd, controller;
    DWORD controllerPid;
    RECT restore;
    LONG_PTR oldExStyle;
    ULONGLONG deadline;
    UINT_PTR timer;
    bool hidden=true;
    UINT releaseMessage;
};

static void Restore(WindowGuard* g, const wchar_t* why) {
    if (!g->hidden) return;
    g->hidden=false;
    SetPropW(g->hwnd,GuardProp,reinterpret_cast<HANDLE>(2)); RemovePropW(g->hwnd,DoneProp);
    KillTimer(g->hwnd,g->timer);
    // Preserve unrelated style changes made by Explorer while it was guarded.
    LONG_PTR style=GetWindowLongPtrW(g->hwnd,GWL_EXSTYLE);
    constexpr LONG_PTR mask=WS_EX_TOOLWINDOW|WS_EX_APPWINDOW;
    SetWindowLongPtrW(g->hwnd,GWL_EXSTYLE,(style&~mask)|(g->oldExStyle&mask));
    BOOL off=FALSE; DwmSetWindowAttribute(g->hwnd,DWMWA_CLOAK,&off,sizeof(off));
    RECT r=g->restore;
    if (!MonitorFromRect(&r,MONITOR_DEFAULTTONULL)) {
        SystemParametersInfoW(SPI_GETWORKAREA,0,&r,0);
        r.left+=60; r.top+=60;
    }
    SetWindowPos(g->hwnd,nullptr,r.left,r.top,0,0,SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE|SWP_SHOWWINDOW|SWP_FRAMECHANGED);
    Log(why,g->hwnd);
}

static LRESULT CALLBACK GuardProc(HWND hwnd,UINT msg,WPARAM wp,LPARAM lp,UINT_PTR id,DWORD_PTR ref) {
    auto g=reinterpret_cast<WindowGuard*>(ref);
    if (msg==WM_NCDESTROY) {
        RemoveWindowSubclass(hwnd,GuardProc,id);
        KillTimer(hwnd,g->timer);
        RemovePropW(hwnd,GuardProp); RemovePropW(hwnd,DoneProp);
        Log(L"destroy",hwnd,g->hidden);
        delete g;
        InterlockedDecrement(&objects);
        return DefSubclassProc(hwnd,msg,wp,lp);
    }
    if (msg==g->releaseMessage) Restore(g,L"released");
    if (g->hidden && msg==WM_TIMER && wp==g->timer) {
        DWORD pid=0; GetWindowThreadProcessId(g->controller,&pid);
        if (!IsWindow(g->controller) || pid!=g->controllerPid) Restore(g,L"controller-gone");
        else if (GetTickCount64()>=g->deadline) Restore(g,L"timeout");
    }
    if (g->hidden && msg==WM_WINDOWPOSCHANGING && lp) {
        auto pos=reinterpret_cast<WINDOWPOS*>(lp);
        if (!(pos->flags&SWP_NOMOVE) && pos->x<30000 && pos->y<30000) {
            g->restore.left=pos->x; g->restore.top=pos->y;
        }
        pos->flags&=~SWP_NOMOVE;
        pos->x=32767; pos->y=32767;
    }
    return DefSubclassProc(hwnd,msg,wp,lp);
}

struct OtherWindow { HWND self; bool found; };
static BOOL CALLBACK FindOther(HWND h,LPARAM data) {
    auto s=reinterpret_cast<OtherWindow*>(data);
    wchar_t cls[64]; GetClassNameW(h,cls,64);
    if (h!=s->self && wcscmp(cls,L"CabinetWClass")==0 && IsWindowVisible(h) && !GetPropW(h,GuardProp)) {
        s->found=true; return FALSE;
    }
    return TRUE;
}

static void Attach(HWND hwnd) {
    Log(L"site",hwnd);
    if (!hwnd || GetPropW(hwnd,GuardProp)) return;
    // Never touch an already presented window (e.g. adding another tab).
    if (IsWindowVisible(hwnd)) { Log(L"skip-visible",hwnd); return; }
    DWORD pid=0;
    if (GetWindowThreadProcessId(hwnd,&pid)!=GetCurrentThreadId() || pid!=GetCurrentProcessId()) {
        Log(L"skip-thread",hwnd); return;
    }
    if ((GetAsyncKeyState(VK_CONTROL)&0x8000) && (GetAsyncKeyState(VK_SHIFT)&0x8000)) return;
    HWND controller=Controller();
    if (!controller) { Log(L"no-controller",hwnd); return; }
    OtherWindow other{hwnd,false}; EnumWindows(FindOther,reinterpret_cast<LPARAM>(&other));
    if (!other.found) { Log(L"first-window",hwnd); return; }
    // COM may release the BHO and unload unused DLLs inside DefSubclassProc
    // during NCDESTROY, while this callback is still on the stack. Pin only
    // when actually installing a callback; inert BHO instances remain unloadable.
    HMODULE keepAlive=nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,
        reinterpret_cast<LPCWSTR>(&GuardProc),&keepAlive)) { Log(L"pin-failed",hwnd,GetLastError()); return; }
    auto g=new(std::nothrow) WindowGuard{};
    if (!g) return;
    g->hwnd=hwnd; g->controller=controller;
    GetWindowThreadProcessId(controller,&g->controllerPid);
    GetWindowRect(hwnd,&g->restore);
    g->oldExStyle=GetWindowLongPtrW(hwnd,GWL_EXSTYLE);
    g->deadline=GetTickCount64()+8000;
    g->releaseMessage=RegisterWindowMessageW(ReleaseName);
    g->timer=0x45545531;
    if (!SetWindowSubclass(hwnd,GuardProc,0x45545531,reinterpret_cast<DWORD_PTR>(g))) {delete g; return;}
    InterlockedIncrement(&objects);
    if (!SetTimer(hwnd,g->timer,100,nullptr)) {
        RemoveWindowSubclass(hwnd,GuardProc,0x45545531); delete g; InterlockedDecrement(&objects); return;
    }
    SetPropW(hwnd,GuardProp,reinterpret_cast<HANDLE>(1));
    SetWindowLongPtrW(hwnd,GWL_EXSTYLE,(g->oldExStyle|WS_EX_TOOLWINDOW)&~WS_EX_APPWINDOW);
    BOOL on=TRUE; HRESULT hr=DwmSetWindowAttribute(hwnd,DWMWA_CLOAK,&on,sizeof(on));
    SetWindowPos(hwnd,nullptr,32767,32767,0,0,SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE|SWP_FRAMECHANGED);
    Log(L"guarded",hwnd,static_cast<int>(hr));
}

class Bridge final : public IObjectWithSite {
    LONG refs=1;
    IUnknown* site=nullptr;
public:
    Bridge(){InterlockedIncrement(&objects);}
    ~Bridge(){if(site)site->Release(); InterlockedDecrement(&objects);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid,void** out) override {
        if(!out)return E_POINTER; *out=nullptr;
        if(riid==IID_IUnknown || riid==IID_IObjectWithSite) {*out=static_cast<IObjectWithSite*>(this);AddRef();return S_OK;}
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override {return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release() override {LONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}
    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* value) override {
        if(site){site->Release();site=nullptr;}
        if(!value || !IsExplorer())return S_OK;
        site=value; site->AddRef();
        IWebBrowser2* browser=nullptr;
        HRESULT hr=site->QueryInterface(IID_PPV_ARGS(&browser));
        if(SUCCEEDED(hr)) {
            SHANDLE_PTR raw=0; hr=browser->get_HWND(&raw); browser->Release();
            HWND root=GetAncestor(reinterpret_cast<HWND>(raw),GA_ROOT);
            wchar_t cls[64]{}; GetClassNameW(root,cls,64);
            if(SUCCEEDED(hr) && wcscmp(cls,L"CabinetWClass")==0)Attach(root);
            else Log(L"no-cabinet",root,static_cast<int>(hr));
        } else Log(L"no-browser",nullptr,static_cast<int>(hr));
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetSite(REFIID riid,void** out) override {
        if(!out)return E_POINTER; *out=nullptr;
        return site?site->QueryInterface(riid,out):E_FAIL;
    }
};

class Factory final : public IClassFactory {
    LONG refs=1;
public:
    Factory(){InterlockedIncrement(&objects);}
    ~Factory(){InterlockedDecrement(&objects);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid,void** out) override {
        if(!out)return E_POINTER; *out=nullptr;
        if(riid==IID_IUnknown || riid==IID_IClassFactory){*out=static_cast<IClassFactory*>(this);AddRef();return S_OK;}return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override {return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release() override {LONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer,REFIID riid,void** out) override {
        if(!out)return E_POINTER; *out=nullptr;
        if(outer)return CLASS_E_NOAGGREGATION;
        auto b=new(std::nothrow) Bridge; if(!b)return E_OUTOFMEMORY;
        HRESULT hr=b->QueryInterface(riid,out); b->Release();return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override {if(lock)InterlockedIncrement(&objects);else InterlockedDecrement(&objects);return S_OK;}
};
STDAPI DllCanUnloadNow(){return objects==0?S_OK:S_FALSE;}
STDAPI DllGetClassObject(REFCLSID cls,REFIID riid,void** out){
    if(cls!=CLSID_Bridge)return CLASS_E_CLASSNOTAVAILABLE;
    auto f=new(std::nothrow) Factory;if(!f)return E_OUTOFMEMORY;
    HRESULT hr=f->QueryInterface(riid,out);f->Release();return hr;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){module=h;DisableThreadLibraryCalls(h);}return TRUE;}
#ifdef ETU_LIFETIME_TEST
extern "C" __declspec(dllexport) void TestAttach(HWND hwnd){Attach(hwnd);}
#endif
