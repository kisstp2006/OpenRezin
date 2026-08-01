// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0)
// See LICENSE.txt

using Core.System;
using Foundation.Logger;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using EngineColor = Foundation.Math.Color;

namespace Rendering;

public sealed class OpenGLRenderer : IRenderer, IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core

        out vec3 fragColor;

        // Same hardcoded triangle as in the Vulkan shader - keeps both backends comparable.
        const vec2 positions[3] = vec2[3](
            vec2( 0.0, -0.5),
            vec2( 0.5,  0.5),
            vec2(-0.5,  0.5)
        );

        const vec3 colors[3] = vec3[3](
            vec3(1.0, 0.0, 0.0),
            vec3(0.0, 1.0, 0.0),
            vec3(0.0, 0.0, 1.0)
        );

        void main()
        {
            gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0);
            fragColor = colors[gl_VertexID];
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core

        in vec3 fragColor;
        out vec4 outColor;

        void main()
        {
            outColor = vec4(fragColor, 1.0);
        }
        """;

    private readonly SilkWindow window;
    private readonly GL gl;

    private readonly uint vertexArray;
    private readonly uint shaderProgram;

    private bool disposed;

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

        shaderProgram = CreateShaderProgram(
            VertexShaderSource,
            FragmentShaderSource);

        // Core profile requires a bound VAO for any draw call, even though we have
        // no vertex buffers - the shader generates positions from gl_VertexID.
        vertexArray = gl.GenVertexArray();
        gl.BindVertexArray(vertexArray);

        var framebufferSize = window.NativeWindow.FramebufferSize;
        Resize(framebufferSize.X, framebufferSize.Y);

        Log.Info("OpenGLRenderer initialized successfully.");
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

    public void Present()
    {
        ThrowIfDisposed();

        gl.UseProgram(shaderProgram);
        gl.BindVertexArray(vertexArray);

        // The vertex shader builds the 3 vertices from gl_VertexID,
        // so no vertex buffer is bound here.
        gl.DrawArrays(
            PrimitiveType.Triangles,
            0,
            3);

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

    private uint CreateShaderProgram(
        string vertexSource,
        string fragmentSource)
    {
        uint vertexShader = CompileShader(
            ShaderType.VertexShader,
            vertexSource);

        uint fragmentShader = CompileShader(
            ShaderType.FragmentShader,
            fragmentSource);

        uint program = gl.CreateProgram();

        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);

        // GetProgram writes 0/1 into an int rather than returning a bool.
        gl.GetProgram(
            program,
            GLEnum.LinkStatus,
            out int success);

        if (success == 0)
        {
            string infoLog = gl.GetProgramInfoLog(program);

            gl.DeleteProgram(program);
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);

            Log.Error($"OpenGL shader linking failed: {infoLog}");

            throw new InvalidOperationException(
                $"OpenGL shader linking failed:\n{infoLog}");
        }

        gl.DetachShader(program, vertexShader);
        gl.DetachShader(program, fragmentShader);

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);

        return program;
    }

    private uint CompileShader(
        ShaderType shaderType,
        string source)
    {
        uint shader = gl.CreateShader(shaderType);

        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(
            shader,
            ShaderParameterName.CompileStatus,
            out int success);

        if (success == 0)
        {
            string infoLog = gl.GetShaderInfoLog(shader);

            gl.DeleteShader(shader);

            Log.Error(
                $"OpenGL {shaderType} compilation failed: {infoLog}");

            throw new InvalidOperationException(
                $"OpenGL {shaderType} compilation failed:\n{infoLog}");
        }

        return shader;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        // GL object deletion needs the owning context current, same as creation.
        window.NativeWindow.MakeCurrent();

        gl.DeleteVertexArray(vertexArray);
        gl.DeleteProgram(shaderProgram);
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