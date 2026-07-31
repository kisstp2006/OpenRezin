// TinyTui.cs
// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TinyTui;

[Flags]
public enum TextDecoration
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Inverse = 4
}

public enum TuiColor
{
    Default = -1,
    Black = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7,
    BrightBlack = 8,
    BrightRed = 9,
    BrightGreen = 10,
    BrightYellow = 11,
    BrightBlue = 12,
    BrightMagenta = 13,
    BrightCyan = 14,
    BrightWhite = 15
}

public readonly record struct TuiStyle(
    TuiColor Foreground = TuiColor.Default,
    TuiColor Background = TuiColor.Default,
    TextDecoration Decoration = TextDecoration.None)
{
    public static readonly TuiStyle Default = new();
}

public readonly record struct TuiPoint(int X, int Y);

public readonly record struct TuiSize(int Width, int Height)
{
    public static readonly TuiSize Empty = new(0, 0);
}

public readonly record struct TuiRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) =>
        x >= Left && y >= Top && x < Right && y < Bottom;

    public TuiRect Intersect(TuiRect other)
    {
        int left = Math.Max(Left, other.Left);
        int top = Math.Max(Top, other.Top);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? new TuiRect(left, top, 0, 0)
            : new TuiRect(left, top, right - left, bottom - top);
    }

    public TuiRect Inset(int left, int top, int right, int bottom)
    {
        int width = Math.Max(0, Width - left - right);
        int height = Math.Max(0, Height - top - bottom);
        return new TuiRect(X + left, Y + top, width, height);
    }
}

public readonly record struct TuiThickness(int Left, int Top, int Right, int Bottom)
{
    public TuiThickness(int all) : this(all, all, all, all) { }
    public TuiThickness(int horizontal, int vertical) : this(horizontal, vertical, horizontal, vertical) { }
    public static readonly TuiThickness Zero = new(0);
}

public enum Dock
{
    None,
    Left,
    Top,
    Right,
    Bottom,
    Fill
}

public enum Orientation
{
    Vertical,
    Horizontal
}

public enum HorizontalTextAlignment
{
    Left,
    Center,
    Right
}

public readonly record struct TuiCell(char Character, TuiStyle Style)
{
    public static readonly TuiCell Empty = new(' ', TuiStyle.Default);
}

public sealed class TuiCanvas
{
    private readonly TuiCell[] _cells;
    private readonly Stack<TuiRect> _clips = new();

    public int Width { get; }
    public int Height { get; }
    public TuiRect Bounds => new(0, 0, Width, Height);
    public TuiRect Clip => _clips.Count == 0 ? Bounds : _clips.Peek();

    internal TuiCell[] Cells => _cells;

    public TuiCanvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _cells = new TuiCell[Width * Height];
        Clear();
    }

    public void Clear(TuiStyle style = default)
    {
        var cell = new TuiCell(' ', style);
        Array.Fill(_cells, cell);
    }

    public IDisposable PushClip(TuiRect clip)
    {
        TuiRect effective = Clip.Intersect(clip);
        _clips.Push(effective);
        return new ClipScope(this);
    }

    public void Set(int x, int y, char character, TuiStyle style = default)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || !Clip.Contains(x, y))
            return;

        if (character == '\0' || character == '\r' || character == '\n' || char.IsControl(character))
            character = ' ';

        _cells[y * Width + x] = new TuiCell(character, style);
    }

    public TuiCell Get(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return TuiCell.Empty;
        return _cells[y * Width + x];
    }

    public void Fill(TuiRect rect, char character = ' ', TuiStyle style = default)
    {
        var r = Clip.Intersect(rect).Intersect(Bounds);
        if (r.IsEmpty) return;

        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
                _cells[y * Width + x] = new TuiCell(character, style);
    }

    public void Write(int x, int y, string? text, TuiStyle style = default, int maxWidth = int.MaxValue)
    {
        if (string.IsNullOrEmpty(text) || y < Clip.Top || y >= Clip.Bottom || maxWidth <= 0)
            return;

        int px = x;
        int written = 0;
        foreach (char c in text)
        {
            if (written >= maxWidth || px >= Clip.Right)
                break;

            if (c == '\n') break;
            if (c != '\r' && px >= Clip.Left)
                Set(px, y, c, style);

            px++;
            written++;
        }
    }

    public void WriteAligned(TuiRect rect, string? text, HorizontalTextAlignment alignment, TuiStyle style = default)
    {
        text ??= string.Empty;
        if (rect.IsEmpty) return;

        string line = text.Replace("\r", string.Empty).Split('\n')[0];
        if (line.Length > rect.Width)
            line = line[..rect.Width];

        int x = alignment switch
        {
            HorizontalTextAlignment.Center => rect.X + Math.Max(0, (rect.Width - line.Length) / 2),
            HorizontalTextAlignment.Right => rect.Right - line.Length,
            _ => rect.X
        };
        Write(x, rect.Y, line, style, rect.Width);
    }

    public void DrawHorizontalLine(int x, int y, int width, char character = '─', TuiStyle style = default)
    {
        for (int i = 0; i < width; i++) Set(x + i, y, character, style);
    }

    public void DrawVerticalLine(int x, int y, int height, char character = '│', TuiStyle style = default)
    {
        for (int i = 0; i < height; i++) Set(x, y + i, character, style);
    }

    public void DrawBox(TuiRect rect, TuiStyle style = default, string? title = null)
    {
        if (rect.Width < 2 || rect.Height < 2) return;

        Set(rect.Left, rect.Top, '┌', style);
        Set(rect.Right - 1, rect.Top, '┐', style);
        Set(rect.Left, rect.Bottom - 1, '└', style);
        Set(rect.Right - 1, rect.Bottom - 1, '┘', style);
        DrawHorizontalLine(rect.Left + 1, rect.Top, rect.Width - 2, '─', style);
        DrawHorizontalLine(rect.Left + 1, rect.Bottom - 1, rect.Width - 2, '─', style);
        DrawVerticalLine(rect.Left, rect.Top + 1, rect.Height - 2, '│', style);
        DrawVerticalLine(rect.Right - 1, rect.Top + 1, rect.Height - 2, '│', style);

        if (!string.IsNullOrWhiteSpace(title) && rect.Width > 4)
        {
            string caption = $" {title} ";
            Write(rect.Left + 2, rect.Top, caption, style, rect.Width - 4);
        }
    }

    private sealed class ClipScope : IDisposable
    {
        private TuiCanvas? _owner;
        public ClipScope(TuiCanvas owner) => _owner = owner;
        public void Dispose()
        {
            if (_owner is null) return;
            if (_owner._clips.Count > 0) _owner._clips.Pop();
            _owner = null;
        }
    }
}

public abstract class TuiView
{
    private readonly List<TuiView> _children = new();
    private TuiRect _frame;

    public TuiView? Parent { get; private set; }
    public IReadOnlyList<TuiView> Children => _children;

    public TuiRect Frame
    {
        get => _frame;
        set => _frame = value;
    }

    public TuiRect Bounds { get; internal set; }
    public Dock Dock { get; set; } = Dock.None;
    public TuiThickness Margin { get; set; } = TuiThickness.Zero;
    public TuiThickness Padding { get; set; } = TuiThickness.Zero;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool CanFocus { get; protected set; }
    public bool HasFocus { get; internal set; }
    public TuiStyle Style { get; set; } = TuiStyle.Default;
    public TuiStyle FocusStyle { get; set; } = new(TuiColor.Black, TuiColor.White);
    public object? Tag { get; set; }

    public event Action<TuiView, TuiSize>? SizeChanged;
    public event Action<TuiView>? Focused;
    public event Action<TuiView>? Blurred;

    protected TuiView()
    {
        _frame = new TuiRect(0, 0, 0, 0);
    }

    public T Add<T>(T child) where T : TuiView
    {
        if (ReferenceEquals(child, this)) throw new InvalidOperationException("A view cannot contain itself.");
        if (child.Parent != null) child.Parent.Remove(child);
        child.Parent = this;
        _children.Add(child);
        return child;
    }

    public bool Remove(TuiView child)
    {
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        return true;
    }

    public void ClearChildren()
    {
        foreach (var child in _children) child.Parent = null;
        _children.Clear();
    }

    public void Focus() => TuiApplication.Current?.SetFocus(this);

    public virtual bool HandleKey(ConsoleKeyInfo key) => false;

    protected virtual TuiRect GetContentBounds() => Bounds.Inset(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);

    protected virtual void OnRender(TuiCanvas canvas)
    {
        if (!Bounds.IsEmpty) canvas.Fill(Bounds, ' ', Style);
    }

    protected virtual void OnLayoutChildren(TuiRect contentBounds)
    {
        TuiRect remaining = contentBounds;

        foreach (var child in _children.Where(c => c.Visible))
        {
            TuiRect next;
            int desiredW = child.Frame.Width > 0 ? child.Frame.Width : remaining.Width;
            int desiredH = child.Frame.Height > 0 ? child.Frame.Height : remaining.Height;

            switch (child.Dock)
            {
                case Dock.Left:
                    desiredW = Math.Min(Math.Max(0, desiredW), remaining.Width);
                    next = new TuiRect(remaining.X, remaining.Y, desiredW, remaining.Height);
                    remaining = new TuiRect(remaining.X + desiredW, remaining.Y, remaining.Width - desiredW, remaining.Height);
                    break;

                case Dock.Right:
                    desiredW = Math.Min(Math.Max(0, desiredW), remaining.Width);
                    next = new TuiRect(remaining.Right - desiredW, remaining.Y, desiredW, remaining.Height);
                    remaining = new TuiRect(remaining.X, remaining.Y, remaining.Width - desiredW, remaining.Height);
                    break;

                case Dock.Top:
                    desiredH = Math.Min(Math.Max(0, desiredH), remaining.Height);
                    next = new TuiRect(remaining.X, remaining.Y, remaining.Width, desiredH);
                    remaining = new TuiRect(remaining.X, remaining.Y + desiredH, remaining.Width, remaining.Height - desiredH);
                    break;

                case Dock.Bottom:
                    desiredH = Math.Min(Math.Max(0, desiredH), remaining.Height);
                    next = new TuiRect(remaining.X, remaining.Bottom - desiredH, remaining.Width, desiredH);
                    remaining = new TuiRect(remaining.X, remaining.Y, remaining.Width, remaining.Height - desiredH);
                    break;

                case Dock.Fill:
                    next = remaining;
                    remaining = new TuiRect(remaining.Right, remaining.Bottom, 0, 0);
                    break;

                default:
                    int width = child.Frame.Width <= 0 ? Math.Max(0, contentBounds.Width - child.Frame.X) : child.Frame.Width;
                    int height = child.Frame.Height <= 0 ? Math.Max(0, contentBounds.Height - child.Frame.Y) : child.Frame.Height;
                    next = new TuiRect(contentBounds.X + child.Frame.X, contentBounds.Y + child.Frame.Y, width, height);
                    break;
            }

            next = next.Inset(child.Margin.Left, child.Margin.Top, child.Margin.Right, child.Margin.Bottom);
            child.SetBoundsAndLayout(next);
        }
    }

    internal void SetBoundsAndLayout(TuiRect bounds)
    {
        var oldSize = new TuiSize(Bounds.Width, Bounds.Height);
        Bounds = new TuiRect(bounds.X, bounds.Y, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height));
        var newSize = new TuiSize(Bounds.Width, Bounds.Height);
        if (newSize != oldSize) SizeChanged?.Invoke(this, newSize);
        OnLayoutChildren(GetContentBounds());
    }

    internal void RenderRecursive(TuiCanvas canvas)
    {
        if (!Visible || Bounds.IsEmpty) return;
        using var clip = canvas.PushClip(Bounds);
        OnRender(canvas);
        foreach (var child in _children)
            child.RenderRecursive(canvas);
    }

    internal IEnumerable<TuiView> EnumerateDepthFirst()
    {
        yield return this;
        foreach (var child in _children)
            foreach (var nested in child.EnumerateDepthFirst())
                yield return nested;
    }

    internal void RaiseFocused() => Focused?.Invoke(this);
    internal void RaiseBlurred() => Blurred?.Invoke(this);
}

public class TuiPanel : TuiView
{
    public bool Border { get; set; }
    public string? Title { get; set; }
    public TuiStyle BorderStyle { get; set; } = TuiStyle.Default;

    protected override TuiRect GetContentBounds()
    {
        var rect = Border ? Bounds.Inset(1, 1, 1, 1) : Bounds;
        return rect.Inset(Padding.Left, Padding.Top, Padding.Right, Padding.Bottom);
    }

    protected override void OnRender(TuiCanvas canvas)
    {
        canvas.Fill(Bounds, ' ', Style);
        if (Border) canvas.DrawBox(Bounds, BorderStyle, Title);
    }
}

public sealed class TuiWindow : TuiPanel
{
    public TuiWindow()
    {
        Border = true;
        Padding = new TuiThickness(1, 0);
    }

    public TuiWindow(string title) : this() => Title = title;
}

public sealed class TuiLabel : TuiView
{
    public string Text { get; set; } = string.Empty;
    public bool Wrap { get; set; }
    public HorizontalTextAlignment Alignment { get; set; } = HorizontalTextAlignment.Left;

    public TuiLabel() { }
    public TuiLabel(string text) => Text = text;

    protected override void OnRender(TuiCanvas canvas)
    {
        canvas.Fill(Bounds, ' ', Style);
        if (Bounds.IsEmpty || string.IsNullOrEmpty(Text)) return;

        if (!Wrap)
        {
            canvas.WriteAligned(Bounds, Text, Alignment, Style);
            return;
        }

        int y = Bounds.Top;
        foreach (string paragraph in Text.Replace("\r", string.Empty).Split('\n'))
        {
            int start = 0;
            while (start < paragraph.Length && y < Bounds.Bottom)
            {
                int take = Math.Min(Bounds.Width, paragraph.Length - start);
                if (take < paragraph.Length - start)
                {
                    int lastSpace = paragraph.LastIndexOf(' ', start + take - 1, take);
                    if (lastSpace >= start) take = Math.Max(1, lastSpace - start + 1);
                }

                string line = paragraph.Substring(start, take).TrimEnd();
                canvas.WriteAligned(new TuiRect(Bounds.X, y, Bounds.Width, 1), line, Alignment, Style);
                start += take;
                while (start < paragraph.Length && paragraph[start] == ' ') start++;
                y++;
            }
            if (paragraph.Length == 0) y++;
            if (y >= Bounds.Bottom) break;
        }
    }
}

public sealed class TuiButton : TuiView
{
    public string Text { get; set; } = "Button";
    public event Action<TuiButton>? Clicked;

    public TuiButton()
    {
        CanFocus = true;
        Frame = new TuiRect(0, 0, 10, 1);
    }

    public TuiButton(string text) : this() => Text = text;

    public void Click()
    {
        if (Enabled) Clicked?.Invoke(this);
    }

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (!Enabled) return false;
        if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
        {
            Click();
            return true;
        }
        return false;
    }

    protected override void OnRender(TuiCanvas canvas)
    {
        var style = HasFocus ? FocusStyle : Style;
        canvas.Fill(Bounds, ' ', style);
        string content = $"[ {Text} ]";
        canvas.WriteAligned(new TuiRect(Bounds.X, Bounds.Y + Math.Max(0, Bounds.Height / 2), Bounds.Width, 1),
            content, HorizontalTextAlignment.Center, style);
    }
}

public sealed class TuiCheckBox : TuiView
{
    public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; private set; }
    public event Action<TuiCheckBox, bool>? Changed;

    public TuiCheckBox()
    {
        CanFocus = true;
        Frame = new TuiRect(0, 0, 20, 1);
    }

    public TuiCheckBox(string text, bool isChecked = false) : this()
    {
        Text = text;
        IsChecked = isChecked;
    }

    public void SetChecked(bool value)
    {
        if (IsChecked == value) return;
        IsChecked = value;
        Changed?.Invoke(this, value);
    }

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (!Enabled) return false;
        if (key.Key is ConsoleKey.Spacebar or ConsoleKey.Enter)
        {
            SetChecked(!IsChecked);
            return true;
        }
        return false;
    }

    protected override void OnRender(TuiCanvas canvas)
    {
        var style = HasFocus ? FocusStyle : Style;
        canvas.Fill(Bounds, ' ', style);
        canvas.Write(Bounds.X, Bounds.Y, $"[{(IsChecked ? 'x' : ' ')}] {Text}", style, Bounds.Width);
    }
}

public sealed class TuiTextBox : TuiView
{
    private string _text = string.Empty;
    private int _cursor;
    private int _scroll;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _cursor = Math.Clamp(_cursor, 0, _text.Length);
            EnsureCursorVisible();
        }
    }

    public int MaxLength { get; set; } = int.MaxValue;
    public bool ReadOnly { get; set; }
    public char? PasswordChar { get; set; }
    public event Action<TuiTextBox, string>? Changed;
    public event Action<TuiTextBox>? Submitted;

    public TuiTextBox()
    {
        CanFocus = true;
        Frame = new TuiRect(0, 0, 20, 1);
    }

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (!Enabled) return false;

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                if (_cursor > 0) _cursor--;
                EnsureCursorVisible();
                return true;
            case ConsoleKey.RightArrow:
                if (_cursor < _text.Length) _cursor++;
                EnsureCursorVisible();
                return true;
            case ConsoleKey.Home:
                _cursor = 0;
                EnsureCursorVisible();
                return true;
            case ConsoleKey.End:
                _cursor = _text.Length;
                EnsureCursorVisible();
                return true;
            case ConsoleKey.Backspace:
                if (!ReadOnly && _cursor > 0)
                {
                    _text = _text.Remove(_cursor - 1, 1);
                    _cursor--;
                    EnsureCursorVisible();
                    Changed?.Invoke(this, _text);
                }
                return true;
            case ConsoleKey.Delete:
                if (!ReadOnly && _cursor < _text.Length)
                {
                    _text = _text.Remove(_cursor, 1);
                    EnsureCursorVisible();
                    Changed?.Invoke(this, _text);
                }
                return true;
            case ConsoleKey.Enter:
                Submitted?.Invoke(this);
                return true;
        }

        if (!ReadOnly && key.KeyChar >= ' ' && key.KeyChar != '\u007f' && !char.IsControl(key.KeyChar) && _text.Length < MaxLength)
        {
            _text = _text.Insert(_cursor, key.KeyChar.ToString());
            _cursor++;
            EnsureCursorVisible();
            Changed?.Invoke(this, _text);
            return true;
        }

        return false;
    }

    protected override void OnRender(TuiCanvas canvas)
    {
        var style = HasFocus ? FocusStyle : Style;
        canvas.Fill(Bounds, ' ', style);
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        EnsureCursorVisible();
        string display = PasswordChar is char mask ? new string(mask, _text.Length) : _text;
        string visible = _scroll < display.Length ? display[_scroll..] : string.Empty;
        canvas.Write(Bounds.X, Bounds.Y, visible, style, Bounds.Width);

        if (HasFocus && Bounds.Width > 0)
        {
            int cx = Bounds.X + Math.Clamp(_cursor - _scroll, 0, Math.Max(0, Bounds.Width - 1));
            char c = _cursor < display.Length ? display[_cursor] : ' ';
            var cursorStyle = style with { Decoration = style.Decoration | TextDecoration.Inverse };
            canvas.Set(cx, Bounds.Y, c, cursorStyle);
        }
    }

    private void EnsureCursorVisible()
    {
        int width = Math.Max(1, Bounds.Width);
        if (_cursor < _scroll) _scroll = _cursor;
        if (_cursor >= _scroll + width) _scroll = _cursor - width + 1;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _text.Length));
    }
}

public sealed class TuiListView : TuiView
{
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private int _topIndex;

    public IList<string> Items => _items;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int next = _items.Count == 0 ? -1 : Math.Clamp(value, 0, _items.Count - 1);
            if (next == _selectedIndex) return;
            _selectedIndex = next;
            EnsureVisible();
            SelectionChanged?.Invoke(this, _selectedIndex);
        }
    }

    public string? SelectedItem => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;
    public TuiStyle SelectedStyle { get; set; } = new(TuiColor.Black, TuiColor.White);
    public event Action<TuiListView, int>? SelectionChanged;
    public event Action<TuiListView, int>? Activated;

    public TuiListView()
    {
        CanFocus = true;
        Frame = new TuiRect(0, 0, 30, 8);
    }

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items ?? Array.Empty<string>());
        _selectedIndex = _items.Count == 0 ? -1 : Math.Clamp(_selectedIndex < 0 ? 0 : _selectedIndex, 0, _items.Count - 1);
        EnsureVisible();
    }

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (!Enabled || _items.Count == 0) return false;
        int page = Math.Max(1, Bounds.Height);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow: SelectedIndex = Math.Max(0, SelectedIndex - 1); return true;
            case ConsoleKey.DownArrow: SelectedIndex = Math.Min(_items.Count - 1, SelectedIndex + 1); return true;
            case ConsoleKey.PageUp: SelectedIndex = Math.Max(0, SelectedIndex - page); return true;
            case ConsoleKey.PageDown: SelectedIndex = Math.Min(_items.Count - 1, SelectedIndex + page); return true;
            case ConsoleKey.Home: SelectedIndex = 0; return true;
            case ConsoleKey.End: SelectedIndex = _items.Count - 1; return true;
            case ConsoleKey.Enter:
                if (SelectedIndex >= 0) Activated?.Invoke(this, SelectedIndex);
                return true;
            default: return false;
        }
    }

    protected override void OnRender(TuiCanvas canvas)
    {
        canvas.Fill(Bounds, ' ', Style);
        EnsureVisible();
        int rows = Math.Max(0, Bounds.Height);

        for (int row = 0; row < rows; row++)
        {
            int index = _topIndex + row;
            if (index >= _items.Count) break;
            var style = index == SelectedIndex ? (HasFocus ? FocusStyle : SelectedStyle) : Style;
            canvas.Fill(new TuiRect(Bounds.X, Bounds.Y + row, Bounds.Width, 1), ' ', style);
            canvas.Write(Bounds.X, Bounds.Y + row, _items[index], style, Bounds.Width);
        }
    }

    private void EnsureVisible()
    {
        if (_selectedIndex < 0) { _topIndex = 0; return; }
        int rows = Math.Max(1, Bounds.Height);
        if (_selectedIndex < _topIndex) _topIndex = _selectedIndex;
        if (_selectedIndex >= _topIndex + rows) _topIndex = _selectedIndex - rows + 1;
        _topIndex = Math.Clamp(_topIndex, 0, Math.Max(0, _items.Count - rows));
    }
}

public sealed class TuiProgressBar : TuiView
{
    private double _value;
    public double Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0.0, 1.0);
    }

    public bool ShowPercentage { get; set; } = true;
    public TuiStyle FilledStyle { get; set; } = new(TuiColor.Black, TuiColor.Green);

    public TuiProgressBar() => Frame = new TuiRect(0, 0, 20, 1);

    protected override void OnRender(TuiCanvas canvas)
    {
        canvas.Fill(Bounds, ' ', Style);
        if (Bounds.IsEmpty) return;

        int filled = (int)Math.Round(Bounds.Width * Value, MidpointRounding.AwayFromZero);
        if (filled > 0) canvas.Fill(new TuiRect(Bounds.X, Bounds.Y, Math.Min(filled, Bounds.Width), 1), ' ', FilledStyle);
        if (ShowPercentage)
        {
            string text = $"{Value * 100:0}%";
            int x = Bounds.X + Math.Max(0, (Bounds.Width - text.Length) / 2);
            for (int i = 0; i < text.Length && x + i < Bounds.Right; i++)
            {
                var style = x + i < Bounds.X + filled ? FilledStyle : Style;
                canvas.Set(x + i, Bounds.Y, text[i], style);
            }
        }
    }
}

public sealed class TuiStackPanel : TuiView
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public int Spacing { get; set; }

    protected override void OnRender(TuiCanvas canvas) => canvas.Fill(Bounds, ' ', Style);

    protected override void OnLayoutChildren(TuiRect contentBounds)
    {
        int cursor = Orientation == Orientation.Vertical ? contentBounds.Top : contentBounds.Left;

        foreach (var child in Children.Where(c => c.Visible))
        {
            TuiRect next;
            if (Orientation == Orientation.Vertical)
            {
                int height = child.Frame.Height > 0 ? child.Frame.Height : 1;
                int width = child.Frame.Width > 0 ? Math.Min(child.Frame.Width, contentBounds.Width) : contentBounds.Width;
                next = new TuiRect(contentBounds.X, cursor, width, Math.Min(height, Math.Max(0, contentBounds.Bottom - cursor)));
                cursor += height + Spacing;
            }
            else
            {
                int width = child.Frame.Width > 0 ? child.Frame.Width : 1;
                int height = child.Frame.Height > 0 ? Math.Min(child.Frame.Height, contentBounds.Height) : contentBounds.Height;
                next = new TuiRect(cursor, contentBounds.Y, Math.Min(width, Math.Max(0, contentBounds.Right - cursor)), height);
                cursor += width + Spacing;
            }

            next = next.Inset(child.Margin.Left, child.Margin.Top, child.Margin.Right, child.Margin.Bottom);
            child.SetBoundsAndLayout(next);
        }
    }
}

public sealed class TuiApplication : IDisposable
{
    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly TuiRenderer _renderer = new();
    private readonly object _sync = new();
    private bool _running;
    private bool _disposed;
    private bool _terminalEntered;
    private bool _oldTreatControlCAsInput;
    private int _exitCode;
    private TuiView? _root;
    private TuiView? _focused;
    private TuiSize _terminalSize;

    public static TuiApplication? Current { get; private set; }
    public TuiView? Root => _root;
    public TuiView? FocusedView => _focused;
    public bool IsRunning => _running;
    public int FrameIntervalMilliseconds { get; set; } = 16;
    public bool UseAlternateScreen { get; set; } = true;
    public bool HideCursor { get; set; } = true;
    public bool DisableLineWrap { get; set; } = true;
    public bool ExitOnEscape { get; set; }

    public event Action<TuiApplication, TuiSize>? Resized;
    public event Action<TuiApplication, ConsoleKeyInfo>? UnhandledKey;
    public event Action<TuiApplication>? Started;
    public event Action<TuiApplication>? Stopping;

    public int Run(TuiView root, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TuiApplication));
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (_running) throw new InvalidOperationException("The application is already running.");
        if (Console.IsOutputRedirected) throw new InvalidOperationException("TinyTui requires an interactive terminal output.");
        if (Console.IsInputRedirected) throw new InvalidOperationException("TinyTui requires an interactive terminal input.");

        lock (_sync)
        {
            _root = root;
            _running = true;
            _exitCode = 0;
            Current = this;
        }

        try
        {
            EnterTerminal();
            Started?.Invoke(this);
            RefreshTerminalSize(force: true);
            EnsureValidFocus();

            while (_running && !cancellationToken.IsCancellationRequested)
            {
                DrainPostedActions();
                RefreshTerminalSize(force: false);
                ProcessInput();
                EnsureValidFocus();
                Render();
                Thread.Sleep(Math.Max(1, FrameIntervalMilliseconds));
            }

            if (cancellationToken.IsCancellationRequested)
                _exitCode = 1;

            return _exitCode;
        }
        finally
        {
            try { Stopping?.Invoke(this); } catch { }
            _running = false;
            _focused = null;
            _root = null;
            if (ReferenceEquals(Current, this)) Current = null;
            ExitTerminal();
        }
    }

    public void Stop(int exitCode = 0)
    {
        _exitCode = exitCode;
        _running = false;
    }

    public void Post(Action action)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TuiApplication));
        if (action is null) throw new ArgumentNullException(nameof(action));
        _posted.Enqueue(action);
    }

    public bool SetFocus(TuiView? view)
    {
        if (view != null && (!view.CanFocus || !view.Visible || !view.Enabled || !IsAttachedToRoot(view)))
            return false;

        if (ReferenceEquals(_focused, view)) return true;
        var old = _focused;
        _focused = view;
        if (old != null)
        {
            old.HasFocus = false;
            old.RaiseBlurred();
        }
        if (view != null)
        {
            view.HasFocus = true;
            view.RaiseFocused();
        }
        return true;
    }

    public bool FocusNext(bool backwards = false)
    {
        var focusable = GetFocusableViews();
        if (focusable.Count == 0) return SetFocus(null);

        int current = _focused == null ? -1 : focusable.IndexOf(_focused);
        int next;
        if (backwards)
            next = current <= 0 ? focusable.Count - 1 : current - 1;
        else
            next = current < 0 || current >= focusable.Count - 1 ? 0 : current + 1;

        return SetFocus(focusable[next]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        ExitTerminal();
        GC.SuppressFinalize(this);
    }

    private void RefreshTerminalSize(bool force)
    {
        var size = TerminalHost.GetSize();
        if (!force && size == _terminalSize) return;

        _terminalSize = size;
        _renderer.Reset();
        _root?.SetBoundsAndLayout(new TuiRect(0, 0, size.Width, size.Height));
        Resized?.Invoke(this, size);
    }

    private void Render()
    {
        if (_root is null || _terminalSize.Width <= 0 || _terminalSize.Height <= 0) return;
        _root.SetBoundsAndLayout(new TuiRect(0, 0, _terminalSize.Width, _terminalSize.Height));
        var canvas = new TuiCanvas(_terminalSize.Width, _terminalSize.Height);
        _root.RenderRecursive(canvas);
        _renderer.Render(canvas);
    }

    private void ProcessInput()
    {
        while (_running && Console.KeyAvailable)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Tab)
            {
                FocusNext(key.Modifiers.HasFlag(ConsoleModifiers.Shift));
                continue;
            }

            if (ExitOnEscape && key.Key == ConsoleKey.Escape)
            {
                Stop();
                continue;
            }

            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                Stop(130);
                continue;
            }

            bool handled = _focused?.HandleKey(key) == true;
            if (!handled) UnhandledKey?.Invoke(this, key);
        }
    }

    private void DrainPostedActions()
    {
        while (_posted.TryDequeue(out var action)) action();
    }

    private void EnsureValidFocus()
    {
        if (_focused != null && _focused.CanFocus && _focused.Visible && _focused.Enabled && IsAttachedToRoot(_focused))
            return;
        var first = GetFocusableViews().FirstOrDefault();
        SetFocus(first);
    }

    private List<TuiView> GetFocusableViews() =>
        _root?.EnumerateDepthFirst().Where(v => v.CanFocus && v.Visible && v.Enabled && AncestorsVisibleAndEnabled(v)).ToList()
        ?? new List<TuiView>();

    private static bool AncestorsVisibleAndEnabled(TuiView view)
    {
        for (var p = view.Parent; p != null; p = p.Parent)
            if (!p.Visible || !p.Enabled) return false;
        return true;
    }

    private bool IsAttachedToRoot(TuiView view)
    {
        if (_root is null) return false;
        TuiView current = view;
        while (current.Parent != null) current = current.Parent;
        return ReferenceEquals(current, _root);
    }

    private void EnterTerminal()
    {
        if (_terminalEntered) return;
        TerminalHost.EnableVirtualTerminal();
        Console.OutputEncoding = Encoding.UTF8;
        _oldTreatControlCAsInput = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;

        var sb = new StringBuilder();
        if (UseAlternateScreen) sb.Append("\x1b[?1049h");
        if (HideCursor) sb.Append("\x1b[?25l");
        if (DisableLineWrap) sb.Append("\x1b[?7l");
        sb.Append("\x1b[0m\x1b[2J\x1b[H");
        Console.Write(sb.ToString());
        _terminalEntered = true;
    }

    private void ExitTerminal()
    {
        if (!_terminalEntered) return;
        try
        {
            var sb = new StringBuilder("\x1b[0m");
            if (DisableLineWrap) sb.Append("\x1b[?7h");
            if (HideCursor) sb.Append("\x1b[?25h");
            if (UseAlternateScreen) sb.Append("\x1b[?1049l");
            Console.Write(sb.ToString());
            Console.TreatControlCAsInput = _oldTreatControlCAsInput;
            TerminalHost.RestoreVirtualTerminal();
        }
        catch { }
        finally
        {
            _terminalEntered = false;
        }
    }
}

internal sealed class TuiRenderer
{
    private TuiCell[]? _previous;
    private int _width;
    private int _height;

    public void Reset()
    {
        _previous = null;
        _width = 0;
        _height = 0;
    }

    public void Render(TuiCanvas canvas)
    {
        bool full = _previous is null || _width != canvas.Width || _height != canvas.Height;
        if (full)
        {
            _width = canvas.Width;
            _height = canvas.Height;
            _previous = new TuiCell[canvas.Width * canvas.Height];
            Array.Fill(_previous, new TuiCell('\0', new TuiStyle((TuiColor)(-999), (TuiColor)(-999), (TextDecoration)(-999))));
        }

        var output = new StringBuilder();
        TuiStyle? activeStyle = null;
        int cursorX = -1;
        int cursorY = -1;
        var current = canvas.Cells;
        var previous = _previous!;

        for (int y = 0; y < canvas.Height; y++)
        {
            int x = 0;
            while (x < canvas.Width)
            {
                int index = y * canvas.Width + x;
                if (!full && current[index].Equals(previous[index]))
                {
                    x++;
                    continue;
                }

                int runStart = x;
                int runEnd = x + 1;
                while (runEnd < canvas.Width)
                {
                    int ri = y * canvas.Width + runEnd;
                    if (!full && current[ri].Equals(previous[ri])) break;
                    runEnd++;
                }

                if (cursorX != runStart || cursorY != y)
                    output.Append("\x1b[").Append(y + 1).Append(';').Append(runStart + 1).Append('H');

                cursorX = runStart;
                cursorY = y;

                for (int rx = runStart; rx < runEnd; rx++)
                {
                    var cell = current[y * canvas.Width + rx];
                    if (activeStyle is null || activeStyle.Value != cell.Style)
                    {
                        AppendStyle(output, cell.Style);
                        activeStyle = cell.Style;
                    }
                    output.Append(cell.Character);
                    cursorX++;
                }

                x = runEnd;
            }
        }

        if (output.Length > 0)
        {
            output.Append("\x1b[0m");
            Console.Write(output.ToString());
        }

        Array.Copy(current, previous, current.Length);
    }

    private static void AppendStyle(StringBuilder sb, TuiStyle style)
    {
        sb.Append("\x1b[0");
        if (style.Decoration.HasFlag(TextDecoration.Bold)) sb.Append(";1");
        if (style.Decoration.HasFlag(TextDecoration.Underline)) sb.Append(";4");
        if (style.Decoration.HasFlag(TextDecoration.Inverse)) sb.Append(";7");
        AppendColor(sb, style.Foreground, foreground: true);
        AppendColor(sb, style.Background, foreground: false);
        sb.Append('m');
    }

    private static void AppendColor(StringBuilder sb, TuiColor color, bool foreground)
    {
        int value = (int)color;
        if (value < 0) return;
        if (value < 8)
        {
            sb.Append(';').Append((foreground ? 30 : 40) + value);
        }
        else
        {
            sb.Append(';').Append((foreground ? 90 : 100) + (value - 8));
        }
    }
}

internal static class TerminalHost
{
    private static uint? _originalWindowsOutputMode;
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint DisableNewlineAutoReturn = 0x0008;

    public static TuiSize GetSize()
    {
        try
        {
            int width = Math.Max(1, Console.WindowWidth);
            int height = Math.Max(1, Console.WindowHeight);
            return new TuiSize(width, height);
        }
        catch
        {
            return new TuiSize(80, 25);
        }
    }

    public static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            IntPtr handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;
            if (!GetConsoleMode(handle, out uint mode)) return;
            _originalWindowsOutputMode ??= mode;
            SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn);
        }
        catch { }
    }

    public static void RestoreVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows() || _originalWindowsOutputMode is null) return;
        try
        {
            IntPtr handle = GetStdHandle(StdOutputHandle);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                SetConsoleMode(handle, _originalWindowsOutputMode.Value);
        }
        catch { }
        finally
        {
            _originalWindowsOutputMode = null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
