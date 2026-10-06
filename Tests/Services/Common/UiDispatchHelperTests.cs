using System;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using Xunit;

namespace CSharpEditorPlugin.Tests.Services.Common;

public class UiDispatchHelperTests
{
    [Fact]
    public void RunOnUi_InHeadlessTest_ExecutesInline()
    {
        bool executed = false;
        UiDispatchHelper.RunOnUi(() => executed = true);
        Assert.True(executed);
    }

    [Fact]
    public async Task InvokeAsync_Action_InHeadlessTest_ExecutesInline()
    {
        bool executed = false;
        await UiDispatchHelper.InvokeAsync(() => executed = true);
        Assert.True(executed);
    }

    [Fact]
    public async Task InvokeAsync_AsyncAction_InHeadlessTest_ExecutesInline()
    {
        bool executed = false;
        await UiDispatchHelper.InvokeAsync(async () =>
        {
            await Task.Yield();
            executed = true;
        });
        Assert.True(executed);
    }

    [Fact]
    public async Task InvokeAsync_Func_InHeadlessTest_ReturnsValue()
    {
        var result = await UiDispatchHelper.InvokeAsync(() => 42);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeAsync_AsyncFunc_InHeadlessTest_ReturnsValue()
    {
        var result = await UiDispatchHelper.InvokeAsync(async () =>
        {
            await Task.Yield();
            return "web-ready";
        });
        Assert.Equal("web-ready", result);
    }

    [Fact]
    public async Task ImmediateDispatcher_ExecutesAllOperationsInline()
    {
        IDispatcher dispatcher = new ImmediateDispatcher();
        Assert.False(dispatcher.HasLiveUiLifetime);

        bool ranSync = false;
        dispatcher.RunOnUi(() => ranSync = true);
        Assert.True(ranSync);

        bool ranAsyncAction = false;
        await dispatcher.InvokeAsync(() => ranAsyncAction = true);
        Assert.True(ranAsyncAction);

        var valSync = await dispatcher.InvokeAsync(() => 123);
        Assert.Equal(123, valSync);

        var valAsync = await dispatcher.InvokeAsync(async () =>
        {
            await Task.Yield();
            return 456;
        });
        Assert.Equal(456, valAsync);
    }

    [Fact]
    public void Dispatcher_Current_CanBeCustomizedForWebMode()
    {
        var original = UiDispatchHelper.Current;
        try
        {
            var immediate = new ImmediateDispatcher();
            UiDispatchHelper.Current = immediate;
            Assert.Same(immediate, UiDispatchHelper.Current);
        }
        finally
        {
            UiDispatchHelper.Current = original;
        }
    }
}
