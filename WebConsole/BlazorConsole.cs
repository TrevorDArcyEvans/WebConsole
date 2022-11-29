namespace WebConsole;

using System.Text;
using System.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// stolen from:
//    https://github.com/ZacharyPatten/dotnet-console-games/blob/6cb5187bcdca5b3c53d036ca7cf7673848830514/Projects/Website/BlazorConsole.cs
public sealed class BlazorConsole
{
  private struct Pixel
  {
    public char Char;
    public ConsoleColor BackgroundColor;
    public ConsoleColor ForegroundColor;

    public static bool operator ==(Pixel a, Pixel b) =>
      a.Char == b.Char &&
      a.ForegroundColor == b.ForegroundColor &&
      a.BackgroundColor == b.BackgroundColor;

    public static bool operator !=(Pixel a, Pixel b) => !(a == b);

    public override bool Equals(object? obj) => obj is Pixel pixel && this == pixel;

    public override int GetHashCode() => HashCode.Combine(Char, ForegroundColor, BackgroundColor);
  }

#pragma warning disable CA2211 // Non-constant fields should not be visible
  public static BlazorConsole ActiveConsole = new();
#pragma warning restore CA2211 // Non-constant fields should not be visible

  public const int Delay = 1; // milliseconds
  public const int InactiveDelay = 1000; // milliseconds

  public const int LargestWindowWidth = 120;
  public const int LargestWindowHeight = 51;
  public int CursorLeft { get; private set; }
  public int CursorTop { get; private set; }

  public string Title { get; set; }
  public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
  public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
  public bool CursorVisible { get; set; } = true;

  public Action TriggerRefresh { get; set; }

  private readonly Queue<ConsoleKeyInfo> _inputBuffer = new();
  private bool _refreshOnInputOnly = true;
  private Pixel[,] _view;
  private bool _stateHasChanged = true;

  private int _windowHeight = 35;

  public int WindowHeight
  {
    get => _windowHeight;

    set
    {
      _windowHeight = value;
      HandleResize();
    }
  }

  private int _windowWidth = 80;

  public int WindowWidth
  {
    get => _windowWidth;

    set
    {
      _windowWidth = value;
      HandleResize();
    }
  }

  public int BufferWidth
  {
    get => WindowWidth;

    set => WindowWidth = value;
  }

  public int BufferHeight
  {
    get => WindowHeight;

    set => WindowHeight = value;
  }

  private BlazorConsole()
  {
    _view = new Pixel[WindowHeight, WindowWidth];
    ClearNoRefresh();
  }

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE0060 // Remove unused parameter

  public void SetWindowPosition(int left, int top)
  {
    // do nothing :)
  }

#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CA1822 // Mark members as static

  public void SetWindowSize(int width, int height)
  {
    WindowWidth = width;
    WindowHeight = height;
  }

  public void SetBufferSize(int width, int height) => SetWindowSize(width, height);

  public void EnqueueInput(ConsoleKey key, bool shift = false, bool alt = false, bool control = false)
  {
    var c = key switch
    {
      >= ConsoleKey.A and <= ConsoleKey.Z => (char) (key - ConsoleKey.A + 'a'),
      >= ConsoleKey.D0 and <= ConsoleKey.D9 => (char) (key - ConsoleKey.D0 + '0'),
      ConsoleKey.Enter => '\n',
      ConsoleKey.Backspace => '\b',
      ConsoleKey.OemPeriod => '.',
      _ => '\0',
    };
    _inputBuffer.Enqueue(new(shift ? char.ToUpper(c) : c, key, shift, alt, control));
  }

  public void OnKeyDown(KeyboardEventArgs e)
  {
    switch (e.Key)
    {
      case "Home":
        EnqueueInput(ConsoleKey.Home);
        break;
      case "End":
        EnqueueInput(ConsoleKey.End);
        break;
      case "Backspace":
        EnqueueInput(ConsoleKey.Backspace);
        break;
      case " ":
        EnqueueInput(ConsoleKey.Spacebar);
        break;
      case "Delete":
        EnqueueInput(ConsoleKey.Delete);
        break;
      case "Enter":
        EnqueueInput(ConsoleKey.Enter);
        break;
      case "Escape":
        EnqueueInput(ConsoleKey.Escape);
        break;
      case "ArrowLeft":
        EnqueueInput(ConsoleKey.LeftArrow);
        break;
      case "ArrowRight":
        EnqueueInput(ConsoleKey.RightArrow);
        break;
      case "ArrowUp":
        EnqueueInput(ConsoleKey.UpArrow);
        break;
      case "ArrowDown":
        EnqueueInput(ConsoleKey.DownArrow);
        break;
      case ".":
        EnqueueInput(ConsoleKey.OemPeriod);
        break;
      default:
        if (e.Key.Length is 1)
        {
          var c = e.Key[0];
          switch (c)
          {
            case >= '0' and <= '9':
              EnqueueInput(ConsoleKey.D0 + (c - '0'));
              break;
            case >= 'a' and <= 'z':
              EnqueueInput(ConsoleKey.A + (c - 'a'));
              break;
            case >= 'A' and <= 'Z':
              EnqueueInput(ConsoleKey.A + (c - 'A'), shift: true);
              break;
          }
        }

        break;
    }
  }

  private static string HtmlEncode(ConsoleColor color)
  {
    return color switch
    {
      ConsoleColor.Black => "#000000",
      ConsoleColor.White => "#ffffff",
      ConsoleColor.Blue => "#0000ff",
      ConsoleColor.Red => "#ff0000",
      ConsoleColor.Green => "#00ff00",
      ConsoleColor.Yellow => "#ffff00",
      ConsoleColor.Cyan => "#00ffff",
      ConsoleColor.Magenta => "#ff00ff",
      ConsoleColor.Gray => "#808080",
      ConsoleColor.DarkBlue => "#00008b",
      ConsoleColor.DarkRed => "#8b0000",
      ConsoleColor.DarkGreen => "#006400",
      ConsoleColor.DarkYellow => "#8b8000",
      ConsoleColor.DarkCyan => "#008b8b",
      ConsoleColor.DarkMagenta => "#8b008b",
      ConsoleColor.DarkGray => "#a9a9a9",
      _ => throw new NotImplementedException(),
    };
  }

  public void ResetColor()
  {
    BackgroundColor = ConsoleColor.Black;
    ForegroundColor = ConsoleColor.White;
  }

  public async Task RefreshAndDelay(TimeSpan timeSpan)
  {
    if (_stateHasChanged)
    {
      TriggerRefresh?.Invoke();
    }

    await Task.Delay(timeSpan);
  }

  private void HandleResize()
  {
    if (_view.GetLength(0) != WindowHeight || _view.GetLength(1) != WindowWidth)
    {
      var old_view = _view;
      _view = new Pixel[WindowHeight, WindowWidth];
      for (var row = 0; row < _view.GetLength(0) && row < old_view.GetLength(0); row++)
      {
        for (var column = 0; column < _view.GetLength(1) && column < old_view.GetLength(1); column++)
        {
          _view[row, column] = old_view[row, column];
        }
      }

      _stateHasChanged = true;
    }
  }

  public async Task Refresh()
  {
    if (_stateHasChanged)
    {
      TriggerRefresh?.Invoke();
    }

    await Task.Delay(Delay);
  }

  public MarkupString State
  {
    get
    {
      StringBuilder stateBuilder = new();
      for (var row = 0; row < _view.GetLength(0); row++)
      {
        for (var column = 0; column < _view.GetLength(1); column++)
        {
          if (CursorVisible && (CursorLeft, CursorTop) == (column, row))
          {
            var isDark =
              (_view[row, column].Char is '█' && _view[row, column].ForegroundColor is ConsoleColor.White) ||
              (_view[row, column].Char is ' ' && _view[row, column].BackgroundColor is ConsoleColor.White);
            stateBuilder.Append($@"<span class=""cursor {(isDark ? "cursor-dark" : "cursor-light")}"">");
          }

          if (_view[row, column].BackgroundColor is not ConsoleColor.Black)
          {
            stateBuilder.Append($@"<span style=""background-color:{HtmlEncode(_view[row, column].BackgroundColor)}"">");
          }

          if (_view[row, column].ForegroundColor is not ConsoleColor.White)
          {
            stateBuilder.Append($@"<span style=""color:{HtmlEncode(_view[row, column].ForegroundColor)}"">");
          }

          stateBuilder.Append(HttpUtility.HtmlEncode(_view[row, column].Char));
          if (_view[row, column].ForegroundColor is not ConsoleColor.White)
          {
            stateBuilder.Append("</span>");
          }

          if (_view[row, column].BackgroundColor is not ConsoleColor.Black)
          {
            stateBuilder.Append("</span>");
          }

          if (CursorVisible && (CursorLeft, CursorTop) == (column, row))
          {
            stateBuilder.Append("</span>");
          }
        }

        stateBuilder.Append("<br />");
      }

      var state = stateBuilder.ToString();
      _stateHasChanged = false;
      return (MarkupString) state;
    }
  }

  public void ResetColors()
  {
    BackgroundColor = ConsoleColor.Black;
    ForegroundColor = ConsoleColor.White;
  }

  public async Task Clear()
  {
    ClearNoRefresh();
    if (!_refreshOnInputOnly)
    {
      await Refresh();
    }
  }

  private void ClearNoRefresh()
  {
    for (var row = 0; row < _view.GetLength(0); row++)
    {
      for (var column = 0; column < _view.GetLength(1); column++)
      {
        Pixel pixel = new()
        {
          Char = ' ',
          BackgroundColor = BackgroundColor,
          ForegroundColor = ForegroundColor,
        };
        _stateHasChanged = _stateHasChanged || pixel != _view[row, column];
        _view[row, column] = pixel;
      }
    }

    (CursorLeft, CursorTop) = (0, 0);
  }

  private void WriteNoRefresh(char c)
  {
    if (c is '\r')
    {
      return;
    }

    if (c is '\n')
    {
      WriteLineNoRefresh();
      return;
    }

    if (CursorLeft >= _view.GetLength(1))
    {
      (CursorLeft, CursorTop) = (0, CursorTop + 1);
    }

    if (CursorTop >= _view.GetLength(0))
    {
      for (var row = 0; row < _view.GetLength(0) - 1; row++)
      {
        for (var column = 0; column < _view.GetLength(1); column++)
        {
          _stateHasChanged = _stateHasChanged || _view[row, column] != _view[row + 1, column];
          _view[row, column] = _view[row + 1, column];
        }
      }

      for (var column = 0; column < _view.GetLength(1); column++)
      {
        Pixel pixel = new()
        {
          Char = ' ',
          BackgroundColor = BackgroundColor,
          ForegroundColor = ForegroundColor
        };
        _stateHasChanged = _stateHasChanged || _view[_view.GetLength(0) - 1, column] != pixel;
        _view[_view.GetLength(0) - 1, column] = pixel;
      }

      CursorTop--;
    }

    {
      Pixel pixel = new()
      {
        Char = c,
        BackgroundColor = BackgroundColor,
        ForegroundColor = ForegroundColor
      };
      _stateHasChanged = _stateHasChanged || _view[CursorTop, CursorLeft] != pixel;
      _view[CursorTop, CursorLeft] = pixel;
    }
    CursorLeft++;
  }

  private void WriteLineNoRefresh()
  {
    while (CursorLeft < _view.GetLength(1))
    {
      WriteNoRefresh(' ');
    }

    (CursorLeft, CursorTop) = (0, CursorTop + 1);
  }

  public async Task Write(object o)
  {
    if (o is null)
    {
      return;
    }

    var s = o.ToString();
    if (s is null or "")
    {
      return;
    }

    foreach (var c in s)
    {
      WriteNoRefresh(c);
    }

    if (!_refreshOnInputOnly)
    {
      await Refresh();
    }
  }

  public async Task WriteLine()
  {
    WriteLineNoRefresh();
    await Refresh();
  }

  public async Task WriteLine(object o)
  {
    if (o is not null)
    {
      var s = o.ToString();
      if (s is not null)
      {
        foreach (var c in s)
        {
          WriteNoRefresh(c);
        }
      }
    }

    WriteLineNoRefresh();
    if (!_refreshOnInputOnly)
    {
      await Refresh();
    }
  }

  private ConsoleKeyInfo ReadKeyNoRefresh(bool capture)
  {
    if (!KeyAvailableNoRefresh())
    {
      throw new InvalidOperationException("attempting a no refresh ReadKey with an empty input buffer");
    }

    var keyInfo = _inputBuffer.Dequeue();
    if (capture is false)
    {
      switch (keyInfo.KeyChar)
      {
        case '\n':
          WriteLineNoRefresh();
          break;

        case '\0':
          break;

        case '\b':
          throw new NotImplementedException("ReadKey backspace not implemented");
        default:

          WriteNoRefresh(keyInfo.KeyChar);
          break;
      }
    }

    return keyInfo;
  }

  public async Task<ConsoleKeyInfo> ReadKey(bool capture)
  {
    while (!KeyAvailableNoRefresh())
    {
      await Refresh();
    }

    return ReadKeyNoRefresh(capture);
  }

  public async Task<string> ReadLine()
  {
    var line = string.Empty;
    while (true)
    {
      while (!KeyAvailableNoRefresh())
      {
        await Refresh();
      }

      var keyInfo = _inputBuffer.Dequeue();
      switch (keyInfo.Key)
      {
        case ConsoleKey.Backspace:
          if (line.Length > 0)
          {
            if (CursorLeft > 0)
            {
              CursorLeft--;
              _stateHasChanged = true;
              _view[CursorTop, CursorLeft].Char = ' ';
            }

            line = line[..^1];
            await Refresh();
          }

          break;

        case ConsoleKey.Enter:
          WriteLineNoRefresh();
          await Refresh();
          return line;

        default:
          if (keyInfo.KeyChar is not '\0')
          {
            line += keyInfo.KeyChar;
            WriteNoRefresh(keyInfo.KeyChar);
            await Refresh();
          }

          break;
      }
    }
  }

  private bool KeyAvailableNoRefresh()
  {
    return _inputBuffer.Count > 0;
  }

  public async Task<bool> KeyAvailable()
  {
    await Refresh();
    return KeyAvailableNoRefresh();
  }

  public async Task SetCursorPosition(int left, int top)
  {
    (CursorLeft, CursorTop) = (left, top);
    if (!_refreshOnInputOnly)
    {
      await Refresh();
    }
  }

  public async Task PromptPressToContinue(string? prompt = null, ConsoleKey key = ConsoleKey.Enter)
  {
    if (!key.IsDefined())
    {
      throw new ArgumentOutOfRangeException(nameof(key), key, $"{nameof(key)} is not a defined value in the {nameof(ConsoleKey)} enum");
    }

    prompt ??= $"Press [{key}] to continue...";
    foreach (var c in prompt)
    {
      WriteNoRefresh(c);
    }

    await PressToContinue(key);
  }

  public async Task PressToContinue(ConsoleKey key = ConsoleKey.Enter)
  {
    if (!key.IsDefined())
    {
      throw new ArgumentOutOfRangeException(nameof(key), key, $"{nameof(key)} is not a defined value in the {nameof(ConsoleKey)} enum");
    }

    while ((await ReadKey(true)).Key != key)
    {
      continue;
    }
  }

#pragma warning disable CA1822 // Mark members as static
  /// <summary>
  /// Returns true. Some members of <see cref="Console"/> only work
  /// on Windows such as <see cref="Console.WindowWidth"/>, but even though this
  /// is blazor and not necessarily on Windows, this wrapper contains implementations
  /// for those Windows-only members.
  /// </summary>
  /// <returns>true</returns>
  public bool IsWindows() => true;
#pragma warning restore CA1822 // Mark members as static
}
