// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0)
// See LICENSE.txt

using Core.System;
using Foundation.Logger;
using Rendering.Shader;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using EngineColor = Foundation.Math.Color;



namespace Rendering;

public sealed class OpenGLRenderer : IRenderer, IDisposable
{
    

    private readonly SilkWindow window;
    private readonly GL gl;

    private readonly uint vertexArray;
    private readonly uint shaderProgram;


    private bool disposed;
    private readonly uint vertexBuffer;

    private readonly uint cameraUniformBuffer;



    private CameraBufferData currentCameraData = new()
    {
        View = Matrix4x4.Identity,
        Projection = Matrix4x4.Identity
    };


    static readonly Vertex[] vertices = new Vertex[]
        {
            new Vertex { Position = new Vector2(0.0f, -0.5f), Color = new Vector3(1, 0, 0) },
            new Vertex { Position = new Vector2(0.5f,  0.5f), Color = new Vector3(0, 1, 0) },
            new Vertex { Position = new Vector2(-0.5f, 0.5f), Color = new Vector3(0, 0, 1) },
        };

    public OpenGLRenderer(SilkWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        this.window = window;

        // Only set when the window was created with an OpenGL WindowOptions preset -
        // a window created for Vulkan (WindowOptions.DefaultVulkan) won't have one.
        if (window.NativeWindow.GLContext is null)
        {
            throw new InvalidOperationException(
                "The window does not have an OpenGL context.");
        }

        // OpenGL is context-bound per thread - make sure this context is active
        // before issuing any GL calls below.
        window.NativeWindow.MakeCurrent();

        gl = GL.GetApi(window.NativeWindow);

        // Match Vulkan's 0..1 clip-space depth while keeping OpenGL's lower-left origin.
        // The vertex shader compiler handles the separate Y-axis flip.
        gl.ClipControl(
            ClipControlOrigin.LowerLeft,
            ClipControlDepth.ZeroToOne);

        gl.Enable(EnableCap.FramebufferSrgb);

        shaderProgram = CreateShaderProgram();
        cameraUniformBuffer = CreateCameraUniformBuffer();

        // The VAO records the attribute layout, so it must be bound before we configure anything.
        vertexArray = gl.GenVertexArray();
        gl.BindVertexArray(vertexArray);

        vertexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);

        unsafe
        {
            fixed (Vertex* verticesPtr = vertices)
            {
                gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(sizeof(Vertex) * vertices.Length),
                    verticesPtr,
                    BufferUsageARB.StaticDraw);
            }

            // location 0 = POSITION (2 floats at offset 0)
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)0);
            gl.EnableVertexAttribArray(0);

            // location 1 = COLOR0 (3 floats, right after Position)
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)sizeof(Vector2));
            gl.EnableVertexAttribArray(1);
            
        }

        // Initialize the newly allocated UBO with identity matrices so the
        // shader never observes undefined camera data before a scene is loaded.
        SetCamera(currentCameraData);

        var framebufferSize = window.NativeWindow.FramebufferSize;
        Resize(framebufferSize.X, framebufferSize.Y);



        Log.Info("OpenGLRenderer initialized successfully.");
    }

    // Allocates one reusable uniform buffer and binds it to the shader's camera
    // binding point, avoiding per-frame buffer allocation.
    private unsafe uint CreateCameraUniformBuffer()
    {
        uint buffer = gl.GenBuffer();

        gl.BindBuffer(
            BufferTargetARB.UniformBuffer,
            buffer);

        gl.BufferData(
            BufferTargetARB.UniformBuffer,
            (nuint)sizeof(CameraBufferData),
            (void*)null,
            BufferUsageARB.DynamicDraw);

        // Binding point 0 is shared with the shader's CameraBuffer block.
        gl.BindBufferBase(
            BufferTargetARB.UniformBuffer,
            0,
            buffer);

        gl.BindBuffer(
            BufferTargetARB.UniformBuffer,
            0);

        return buffer;
    }

    public void Clear(EngineColor color)
    {
        ThrowIfDisposed();

        gl.ClearColor(
            color.r,
            color.g,
            color.b,
            color.a);

        gl.Clear((uint)ClearBufferMask.ColorBufferBit);
    }

    // Copies the latest matrices into the existing UBO through direct-state
    // access, without temporary managed allocations or GL binding changes.
    public unsafe void SetCamera(in CameraBufferData cameraData)
    {
        ThrowIfDisposed();
        currentCameraData = cameraData;

        fixed (CameraBufferData* dataPtr =
        &currentCameraData)
        {
            gl.NamedBufferSubData(
                cameraUniformBuffer,
                0,
                (nuint)sizeof(CameraBufferData),
                dataPtr);
        }

    }

    public void Present()
    {
        ThrowIfDisposed();

        
        gl.UseProgram(shaderProgram);
        gl.BindVertexArray(vertexArray);

        gl.DrawArrays(
            PrimitiveType.Triangles,
            0,
            (uint)vertices.Length);

        GLEnum error = gl.GetError();
        if (error != GLEnum.NoError)
            Log.Error($"GL error after DrawArrays: {error}");

        window.NativeWindow.SwapBuffers();
    }

    public void Resize(int width, int height)
    {
        ThrowIfDisposed();

        // The framebuffer can be 0x0 while the window is minimized - a 0-sized
        // viewport is invalid, so just skip the update.
        if (width <= 0 || height <= 0)
            return;

        gl.Viewport(
            0,
            0,
            (uint)width,
            (uint)height);
    }

    private uint CreateShaderProgram()
    {
        byte[] vertShaderCode = ShaderCompiler.CompileHlslToSpirv(
            "Shaders/triangle.hlsl",
            "VSMain",
            "vs_6_0",true);

        byte[] fragShaderCode = ShaderCompiler.CompileHlslToSpirv(
            "Shaders/triangle.hlsl",
            "PSMain",
            "ps_6_0",false);

        uint vertexShader = CreateSpirvShader(ShaderType.VertexShader, vertShaderCode, "VSMain");
        uint fragmentShader = CreateSpirvShader(ShaderType.FragmentShader, fragShaderCode, "PSMain");

        uint program = gl.CreateProgram();

        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);

        gl.GetProgram(program, GLEnum.LinkStatus, out int success);

        if (success == 0)
        {
            string infoLog = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
            Log.Error($"OpenGL shader linking failed: {infoLog}");
            throw new InvalidOperationException($"OpenGL shader linking failed:\n{infoLog}");
        }

        gl.DetachShader(program, vertexShader);
        gl.DetachShader(program, fragmentShader);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);


        return program;
    }
    private unsafe uint CreateSpirvShader(ShaderType shaderType, byte[] spirv, string entryPoint)
    {
        uint shader = gl.CreateShader(shaderType);

        fixed (byte* spirvPtr = spirv)
        {
            gl.ShaderBinary(1, &shader, GLEnum.ShaderBinaryFormatSpirV, spirvPtr, (uint)spirv.Length);
        }

        gl.SpecializeShader(shader, entryPoint, 0, (uint*)null, (uint*)null);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);

        if (success == 0)
        {
            string infoLog = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            Log.Error($"OpenGL {shaderType} SPIR-V specialization failed: {infoLog}");
            throw new InvalidOperationException($"OpenGL {shaderType} SPIR-V specialization failed:\n{infoLog}");
        }
        return shader;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        // GL object deletion needs the owning context current, same as creation.
        window.NativeWindow.MakeCurrent();

        gl.DeleteBuffer(cameraUniformBuffer);
        gl.DeleteVertexArray(vertexArray);
        gl.DeleteProgram(shaderProgram);
        gl.DeleteBuffer(vertexBuffer);
        gl.Dispose();

        disposed = true;

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(
                nameof(OpenGLRenderer));
        }
        
    }


}
