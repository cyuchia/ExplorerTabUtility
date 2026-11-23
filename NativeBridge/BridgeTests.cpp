#include "NativeBridge.cpp"
#include <cstdlib>

static void Check(bool ok,const char* name) {
    printf("%s %s\n",ok?"PASS":"FAIL",name);
    if(!ok)exit(1);
}
static void Pump(unsigned ms) {
    ULONGLONG end=GetTickCount64()+ms;
    MSG m{};
    while(GetTickCount64()<end) {
        while(PeekMessageW(&m,nullptr,0,0,PM_REMOVE)){TranslateMessage(&m);DispatchMessageW(&m);}
        Sleep(5);
    }
}
static HWND TestWindow() {
    return CreateWindowExW(WS_EX_TOOLWINDOW,L"CabinetWClass",L"ETU isolated guard test",WS_POPUP,
        50,50,1,1,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
}
int main() {
    WNDCLASSW wc{};wc.lpfnWndProc=DefWindowProcW;wc.hInstance=GetModuleHandleW(nullptr);wc.lpszClassName=L"CabinetWClass";
    Check(RegisterClassW(&wc)!=0,"test-window-class");
    HWND controller=CreateWindowExW(0,L"STATIC",ControllerTitle,0,0,0,0,0,HWND_MESSAGE,nullptr,wc.hInstance,nullptr);
    Check(Controller()==controller,"live-controller-identity");
    HWND other=TestWindow();
    BOOL cloak=TRUE;DwmSetWindowAttribute(other,DWMWA_CLOAK,&cloak,sizeof(cloak));
    SetWindowPos(other,nullptr,32767,32767,0,0,SWP_NOSIZE|SWP_NOACTIVATE|SWP_SHOWWINDOW);
    HWND source=TestWindow();Attach(source);
    Check(GetPropW(source,GuardProp)==reinterpret_cast<HANDLE>(1),"guard-before-show");
    ShowWindow(source,SW_SHOWNOACTIVATE);
    SetWindowPos(source,nullptr,150,150,0,0,SWP_NOSIZE|SWP_NOACTIVATE);
    RECT r{};GetWindowRect(source,&r);
    Check(r.left==32767 && r.top==32767,"show-and-move-stay-offscreen");
    DWORD cloaked=0;DwmGetWindowAttribute(source,DWMWA_CLOAKED,&cloaked,sizeof(cloaked));
    Check((cloaked&1)!=0,"dwm-cloaked");
    SendMessageW(source,RegisterWindowMessageW(ReleaseName),0,0);
    GetWindowRect(source,&r);
    Check(GetPropW(source,GuardProp)==reinterpret_cast<HANDLE>(2) && r.left==150 && r.top==150,"explicit-release-restores-position");
    DwmGetWindowAttribute(source,DWMWA_CLOAKED,&cloaked,sizeof(cloaked));
    Check((cloaked&1)==0,"release-uncloaks");
    DestroyWindow(source);
    source=TestWindow();Attach(source);ShowWindow(source,SW_SHOWNOACTIVATE);
    DestroyWindow(controller);Pump(300);
    Check(GetPropW(source,GuardProp)==reinterpret_cast<HANDLE>(2),"controller-crash-fail-open");
    DestroyWindow(source);
    source=TestWindow();Attach(source);
    Check(!GetPropW(source,GuardProp),"controller-absent-inert");DestroyWindow(source);
    controller=CreateWindowExW(0,L"STATIC",ControllerTitle,0,0,0,0,0,HWND_MESSAGE,nullptr,wc.hInstance,nullptr);
    source=TestWindow();Attach(source);
    DWORD_PTR ref=0;Check(GetWindowSubclass(source,GuardProc,0x45545531,&ref)!=FALSE,"subclass-reference");
    reinterpret_cast<WindowGuard*>(ref)->deadline=GetTickCount64();Pump(300);
    Check(GetPropW(source,GuardProp)==reinterpret_cast<HANDLE>(2),"deadline-fail-open");
    DestroyWindow(source);DestroyWindow(controller);DestroyWindow(other);
    Check(objects==0,"guard-lifetime-cleanup");
    void* factory=nullptr;
    Check(SUCCEEDED(DllGetClassObject(CLSID_Bridge,IID_IClassFactory,&factory)),"com-factory");
    void* bridge=nullptr;
    Check(SUCCEEDED(static_cast<IClassFactory*>(factory)->CreateInstance(nullptr,IID_IObjectWithSite,&bridge)),"com-instance");
    static_cast<IObjectWithSite*>(bridge)->SetSite(nullptr);
    static_cast<IObjectWithSite*>(bridge)->Release();static_cast<IClassFactory*>(factory)->Release();
    Check(DllCanUnloadNow()==S_OK,"com-lifetime-cleanup");
    controller=CreateWindowExW(0,L"STATIC",ControllerTitle,0,0,0,0,0,HWND_MESSAGE,nullptr,wc.hInstance,nullptr);
    other=TestWindow();DwmSetWindowAttribute(other,DWMWA_CLOAK,&cloak,sizeof(cloak));
    SetWindowPos(other,nullptr,32767,32767,0,0,SWP_NOSIZE|SWP_NOACTIVATE|SWP_SHOWWINDOW);
    HMODULE library=LoadLibraryW(L"GuardLifetimeTest.dll");
    Check(library!=nullptr,"load-real-test-dll");
    auto attach=reinterpret_cast<void(*)(HWND)>(GetProcAddress(library,"TestAttach"));
    Check(attach!=nullptr,"get-dll-test-entry");
    source=TestWindow();attach(source);
    Check(GetPropW(source,GuardProp)==reinterpret_cast<HANDLE>(1),"dll-window-callback-installed");
    FreeLibrary(library);
    Check(GetModuleHandleW(L"GuardLifetimeTest.dll")!=nullptr,"dll-survives-host-release-with-pending-callback");
    ShowWindow(source,SW_SHOWNOACTIVATE);DestroyWindow(source);Pump(100);
    Check(GetModuleHandleW(L"GuardLifetimeTest.dll")!=nullptr,"dll-survives-window-teardown-and-return");
    DestroyWindow(other);DestroyWindow(controller);
    return 0;
}
