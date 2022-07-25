using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using Silk.NET.OpenGL;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

var window = CreateWindow();

SetOpenglPixelFormat(window);
var hglrc = StartOpenglRenderingContext(window);

RunMainLoop(window);

Cleanup(hglrc);

static unsafe void RunMainLoop(HWND window)
{
    var startTime = DateTime.UtcNow;
    var gl = new Silk.NET.OpenGL.GL(new WindowsGlNativeContext());
    
    MSG msg;
    var dc = GetDC(window);
    while (!Closed)
    {
        while (PeekMessageW(&msg, window, 0, 0, PM.PM_REMOVE) && !Closed)
        {
            DispatchMessageW(&msg);
        }

        var dTime = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var r = NormalizeSinCos(MathF.Sin(dTime));
        var g = NormalizeSinCos(MathF.Cos(dTime));
        var b = NormalizeSinCos(MathF.Sin(-dTime));

        gl.ClearColor(r, g, b, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        SwapBuffers(dc);

        float NormalizeSinCos(float sinCos)
        {
            return 0.5f + sinCos / 2.0f;
        }
    }

    ReleaseDC(window, dc);
}

static void Render()
{
    
}

[UnmanagedCallersOnly]
static unsafe LRESULT WinProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
{
    if (message == WM.WM_PAINT)
    {
        var ps = new PAINTSTRUCT();
        var deviceContextHandle = BeginPaint(window, &ps);
        EndPaint(window, &ps);
        return 0;
    }

    if (message == WM.WM_CLOSE)
    {
        Closed = true;
        return 0;
    }

    return DefWindowProcW(window, message, wParam, lParam);
}

static unsafe HWND CreateWindow()
{
    var className = "windowClass";

    fixed (char* classNamePtr = className)
    {
        var windowClass = new WNDCLASSEXW();
        windowClass.cbSize = (uint)sizeof(WNDCLASSEXW);
        windowClass.hbrBackground = HBRUSH.NULL;
        windowClass.hCursor = HCURSOR.NULL;
        windowClass.hIcon = HICON.NULL;
        windowClass.hIconSm = HICON.NULL;
        windowClass.hInstance = HINSTANCE.NULL;
        windowClass.lpszClassName = (ushort*)classNamePtr;
        windowClass.lpszMenuName = null;
        windowClass.style = 0;
        windowClass.lpfnWndProc = &WinProc;

        var classId = RegisterClassExW(&windowClass);
    }

    var windowName = "windowName";
    fixed (char* windowNamePtr = windowName)
    fixed (char* classNamePtr = className)
    {
        var width = 500;
        var height = 500;
        var x = 0;
        var y = 0;

        var styles = WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE;
        var exStyles = 0;

        return CreateWindowExW((uint)exStyles,
            (ushort*)classNamePtr,
            (ushort*)windowNamePtr,
            (uint)styles,
            x, y,
            width, height,
            HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
    }
}

static unsafe void SetOpenglPixelFormat(HWND window)
{
    // Contains desired pixel format characteristics
    PIXELFORMATDESCRIPTOR pfd = new();
    
    // the size of the struct
    pfd.nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR);
    
    // hardcoded version of the struct
    pfd.nVersion = 1;
    
    // we will draw to the window, we will draw via opengl, and we will use two buffers to swap between them each frame 
    pfd.dwFlags = PFD.PFD_DRAW_TO_WINDOW | PFD.PFD_SUPPORT_OPENGL | PFD.PFD_DOUBLEBUFFER;
     
    // We expect to use RGBA pixels
    pfd.iPixelType = PFD.PFD_TYPE_RGBA;
    
    // pixels with 3 * 8 = 24 bits for color 
    pfd.cColorBits = 24;
    
    // Depth of z-buffer (we don't actually care about that for now)
    pfd.cDepthBits = 32;

    HDC hdc = GetDC(window);
    int iPixelFormat;

    // get the device context's best, available pixel format match  
    iPixelFormat = ChoosePixelFormat(hdc, &pfd);

    // make that match the device context's current pixel format  
    SetPixelFormat(hdc, iPixelFormat, &pfd);

    ReleaseDC(window, hdc);
}

static HGLRC StartOpenglRenderingContext(HWND window)
{
    var dc = GetDC(window);
    var gctx = wglCreateContext(dc);
    wglMakeCurrent(dc, gctx);
    ReleaseDC(window, dc);

    return gctx;
}

static void Cleanup(HGLRC gctx)
{
    wglDeleteContext(gctx);
}

public class WindowsGlNativeContext : INativeContext
{
    private readonly UnmanagedLibrary _l;

    public WindowsGlNativeContext()
    {
        _l = new UnmanagedLibrary("opengl32.dll");
    }

    public void Dispose()
    {
        _l.Dispose();
    }

    public nint GetProcAddress(string proc, int? slot = null)
    {
        if (TryGetProcAddress(proc, out var address, slot))
        {
            return address;
        }

        throw new InvalidOperationException("No function was found with the name " + proc + ".");
    }

    public unsafe bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
    {
        if (_l.TryLoadFunction(proc, out addr))
        {
            return true;
        }

        // + 1 for null terminated string
        var asciiName = new byte[proc.Length + 1];

        Encoding.ASCII.GetBytes(proc, asciiName);

        fixed (byte* name = asciiName)
        {
            addr = wglGetProcAddress((sbyte*)name);
            if (addr != IntPtr.Zero)
            {
                return true;
            }
        }

        return false;
    }
}

partial class Program
{
    private static bool Closed;
}