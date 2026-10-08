using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

/// <summary>
/// Configuration callbacks and options for <see cref="EditorContextMenu"/>.
/// </summary>
public sealed class EditorContextMenuOptions
{
    public Func<ILanguageDefinition?>? LanguageProvider { get; init; }
    public Action? FormatAction { get; init; }
    public Action? RunAction { get; init; }
    public string RunHeader { get; init; } = "Run Script";
    public Action? DebugAction { get; init; }
    public Action<int>? ToggleBreakpointAction { get; init; }
    public Action? QuickInfoAction { get; init; }
    public Action? ToggleCommentAction { get; init; }
}

/// <summary>
/// Attaches an authentic VS Code-style right-click context menu and standard
/// clipboard/editing keybindings to an AvaloniaEdit <see cref="TextEditor"/>.
/// </summary>
public static class EditorContextMenu
{
    public static ContextMenu Attach(TextEditor editor, EditorContextMenuOptions? options = null)
    {
        options ??= new EditorContextMenuOptions();
        var menu = new ContextMenu();
        var isMac = OperatingSystem.IsMacOS();
        var cmdOrCtrl = isMac ? KeyModifiers.Meta : KeyModifiers.Control;

        // 1. Cut
        var cutItem = CreateMenuItem(
            "Cut",
            Key.X,
            cmdOrCtrl,
            MaterialIconKind.ContentCut,
            () => editor.Cut());
        menu.Items.Add(cutItem);

        // 2. Copy
        var copyItem = CreateMenuItem(
            "Copy",
            Key.C,
            cmdOrCtrl,
            MaterialIconKind.ContentCopy,
            () => editor.Copy());
        menu.Items.Add(copyItem);

        // 3. Paste
        var pasteItem = CreateMenuItem(
            "Paste",
            Key.V,
            cmdOrCtrl,
            MaterialIconKind.ContentPaste,
            () => editor.Paste());
        menu.Items.Add(pasteItem);

        // 4. Select All
        var selectAllItem = CreateMenuItem(
            "Select All",
            Key.A,
            cmdOrCtrl,
            MaterialIconKind.SelectAll,
            () => editor.SelectAll());
        menu.Items.Add(selectAllItem);

        menu.Items.Add(new Separator());

        // 5. Toggle Line Comment
        Action toggleComment = options.ToggleCommentAction ?? (() => ToggleLineComment(editor, options.LanguageProvider?.Invoke()));
        var commentItem = CreateMenuItem(
            "Toggle Line Comment",
            Key.OemQuestion,
            cmdOrCtrl,
            MaterialIconKind.CommentTextOutline,
            toggleComment);
        menu.Items.Add(commentItem);

        // 6. Format Document
        if (options.FormatAction != null)
        {
            var formatItem = CreateMenuItem(
                "Format Document",
                Key.F,
                KeyModifiers.Shift | KeyModifiers.Alt,
                MaterialIconKind.FormatPaint,
                options.FormatAction);
            menu.Items.Add(formatItem);
        }

        // 7. Show Quick Info (Hover symbol)
        if (options.QuickInfoAction != null)
        {
            menu.Items.Add(new Separator());
            var quickInfoItem = CreateMenuItem(
                "Show Quick Info",
                Key.I,
                cmdOrCtrl,
                MaterialIconKind.InformationOutline,
                options.QuickInfoAction);
            menu.Items.Add(quickInfoItem);
        }

        // 8. Run / Debug / Breakpoints (Execution items)
        if (options.RunAction != null || options.DebugAction != null || options.ToggleBreakpointAction != null)
        {
            menu.Items.Add(new Separator());

            if (options.RunAction != null)
            {
                var runItem = CreateMenuItem(
                    options.RunHeader,
                    Key.F5,
                    KeyModifiers.Control,
                    MaterialIconKind.Play,
                    options.RunAction);
                menu.Items.Add(runItem);
            }

            if (options.DebugAction != null)
            {
                var debugItem = CreateMenuItem(
                    "Start Debugging",
                    Key.F5,
                    KeyModifiers.None,
                    MaterialIconKind.BugPlayOutline,
                    options.DebugAction);
                menu.Items.Add(debugItem);
            }

            if (options.ToggleBreakpointAction != null)
            {
                var bpItem = CreateMenuItem(
                    "Toggle Breakpoint",
                    Key.F9,
                    KeyModifiers.None,
                    MaterialIconKind.RecordCircleOutline,
                    () => options.ToggleBreakpointAction(editor.TextArea.Caret.Line));
                menu.Items.Add(bpItem);
            }
        }

        // Update items' enabled states when opening
        void UpdateStates()
        {
            bool hasSelection = editor.SelectionLength > 0;
            bool isReadOnly = editor.IsReadOnly;
            bool hasText = editor.Document != null && editor.Document.TextLength > 0;

            cutItem.IsEnabled = !isReadOnly && hasSelection;
            copyItem.IsEnabled = hasSelection;
            pasteItem.IsEnabled = !isReadOnly;
            selectAllItem.IsEnabled = hasText;
            commentItem.IsEnabled = !isReadOnly && hasText;
        }

        menu.Opening += (_, _) => UpdateStates();
        menu.Opened += (_, _) => UpdateStates();

        editor.ContextMenu = menu;

        // Ensure right-click handles caret position intelligently:
        // - If right-clicked outside selection (or with no selection), move caret to clicked position.
        // - If right-clicked inside selection, preserve selection so Cut/Copy applies to it.
        editor.TextArea.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
        {
            var point = e.GetCurrentPoint(editor.TextArea);
            if (point.Properties.IsRightButtonPressed)
            {
                var textView = editor.TextArea.TextView;
                if (textView != null && editor.Document != null)
                {
                    var clickPos = e.GetPosition(textView) + textView.ScrollOffset;
                    var pos = textView.GetPosition(clickPos);
                    if (pos.HasValue)
                    {
                        int offset = editor.Document.GetOffset(pos.Value.Location);
                        bool insideSelection = editor.SelectionLength > 0 &&
                                               offset >= editor.SelectionStart &&
                                               offset <= (editor.SelectionStart + editor.SelectionLength);
                        if (!insideSelection)
                        {
                            editor.SelectionLength = 0;
                            editor.CaretOffset = offset;
                        }
                    }
                }
            }
        }, RoutingStrategies.Tunnel);

        // Register standard cross-platform / macOS editing KeyBindings
        RegisterKeyBindings(editor, cmdOrCtrl, toggleComment);

        return menu;
    }

    private static void RegisterKeyBindings(TextEditor editor, KeyModifiers cmdOrCtrl, Action toggleComment)
    {
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.C, cmdOrCtrl), Command = new RelayCommand(() => editor.Copy()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.V, cmdOrCtrl), Command = new RelayCommand(() => editor.Paste()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.X, cmdOrCtrl), Command = new RelayCommand(() => editor.Cut()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.A, cmdOrCtrl), Command = new RelayCommand(() => editor.SelectAll()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, cmdOrCtrl), Command = new RelayCommand(() => editor.Undo()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, cmdOrCtrl | KeyModifiers.Shift), Command = new RelayCommand(() => editor.Redo()) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemQuestion, cmdOrCtrl), Command = new RelayCommand(toggleComment) });
        editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Divide, cmdOrCtrl), Command = new RelayCommand(toggleComment) });

        if (!OperatingSystem.IsMacOS())
        {
            editor.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = new RelayCommand(() => editor.Redo()) });
        }
    }

    public static void ToggleLineComment(TextEditor editor, ILanguageDefinition? language)
    {
        if (editor.Document == null || editor.IsReadOnly) return;
        var document = editor.Document;
        var prefix = language?.LineCommentPrefix ?? "//";
        var selection = editor.TextArea.Selection;
        int startLine;
        int endLine;

        if (!selection.IsEmpty)
        {
            startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
        }
        else
        {
            startLine = document.GetLineByOffset(editor.CaretOffset).LineNumber;
            endLine = startLine;
        }

        using (document.RunUpdate())
        {
            bool allCommented = true;
            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length).TrimStart();
                if (!string.IsNullOrEmpty(lineText) && !lineText.StartsWith(prefix, StringComparison.Ordinal))
                {
                    allCommented = false;
                    break;
                }
            }

            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length);
                if (allCommented)
                {
                    int slashIdx = lineText.IndexOf(prefix, StringComparison.Ordinal);
                    if (slashIdx >= 0)
                    {
                        int removeLen = (slashIdx + prefix.Length < lineText.Length && lineText[slashIdx + prefix.Length] == ' ') ? prefix.Length + 1 : prefix.Length;
                        document.Remove(line.Offset + slashIdx, removeLen);
                    }
                }
                else
                {
                    int indent = 0;
                    while (indent < lineText.Length && char.IsWhiteSpace(lineText[indent])) indent++;
                    document.Insert(line.Offset + indent, prefix + " ");
                }
            }
        }
    }

    private static MenuItem CreateMenuItem(
        string header,
        Key? key,
        KeyModifiers modifiers,
        MaterialIconKind iconKind,
        Action onClick)
    {
        var item = new MenuItem
        {
            Header = header
        };
        if (key.HasValue && key.Value != Key.None)
        {
            item.InputGesture = new KeyGesture(key.Value, modifiers);
        }
        item.Icon = new MaterialIcon
        {
            Kind = iconKind,
            Width = 14,
            Height = 14,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        item.Click += (_, _) => onClick();
        return item;
    }
}
