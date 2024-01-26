using System;
using System.Collections.Generic;
using AutoFixture;
using NSubstitute;
using Whim.TestUtils;
using Windows.Win32.Foundation;
using Xunit;

namespace Whim.Tests;

public class WorkspaceCustomization : ICustomization
{
	public void Customize(IFixture fixture)
	{
		IInternalContext internalCtx = fixture.Freeze<IInternalContext>();

		// Assume windows are valid windows.
		internalCtx.CoreNativeManager.IsWindow(Arg.Any<HWND>()).Returns(true);

		// Assume windows are managed.
		internalCtx.WindowManager.HandleWindowMap.ContainsKey(Arg.Any<HWND>()).Returns(true);

		// Set up the triggers.
		WorkspaceManagerTriggers triggers =
			new()
			{
				ActiveLayoutEngineChanged = Substitute.For<Action<ActiveLayoutEngineChangedEventArgs>>(),
				WorkspaceLayoutStarted = Substitute.For<Action<WorkspaceEventArgs>>(),
				WorkspaceLayoutCompleted = Substitute.For<Action<WorkspaceEventArgs>>(),
				WorkspaceRenamed = Substitute.For<Action<WorkspaceRenamedEventArgs>>(),
			};
		fixture.Inject(triggers);
	}
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Reliability",
	"CA2000:Dispose objects before losing scope",
	Justification = "Unnecessary for tests"
)]
public class WorkspaceTests
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0017:Simplify object initialization",
		Justification = "It's a test"
	)]
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void Rename(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When
		workspace.Name = "Workspace2";

		// Then
		Assert.Equal("Workspace2", workspace.Name);
	}

	#region TrySetLayoutEngineFromIndex
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromIndex_LessThanZero(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromIndex(-1);

		// Then
		Assert.False(result);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromIndex_GreaterThanCount(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromIndex(2);

		// Then
		Assert.False(result);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromIndex_AlreadyActive(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		ctx.Butler.GetMonitorForWorkspace(Arg.Any<IWorkspace>()).Returns(null as IMonitor);

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromIndex(0);

		// Then
		Assert.True(result);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromIndex_Success(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromIndex(1);

		// Then
		Assert.True(result);
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.Received(1)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}
	#endregion

	#region TrySetLayoutEngineFromName
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromName_CannotFindEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromName("Layout2");

		// Then
		Assert.False(result);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromName_AlreadyActive(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		ctx.Butler.GetMonitorForWorkspace(Arg.Any<IWorkspace>()).Returns(null as IMonitor);
		layoutEngine.Name.Returns("Layout");

		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromName("Layout");

		// Then
		Assert.True(result);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TrySetLayoutEngineFromName_Success(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		layoutEngine2.Name.Returns("Layout2");

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When
		bool result = workspace.TrySetLayoutEngineFromName("Layout2");

		// Then
		Assert.True(result);
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}
	#endregion

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void Constructor_FailWhenNoLayoutEngines(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers
	)
	{
		Assert.Throws<ArgumentException>(
			() => new Workspace(ctx, internalCtx, triggers, "Workspace", Array.Empty<ILayoutEngine>())
		);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void ContainsWindow_False(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When
		bool result = workspace.ContainsWindow(window);

		// Then
		Assert.False(result);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void ContainsWindow_True_NormalWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);

		// When
		bool result = workspace.ContainsWindow(window);

		// Then
		Assert.True(result);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void ContainsWindow_True_MinimizedWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		workspace.MinimizeWindowStart(window);

		// When
		bool result = workspace.ContainsWindow(window);

		// Then
		Assert.True(result);
	}

	#region WindowFocused
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void WindowFocused_ContainsWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given the window is in the workspace
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);

		// When
		workspace.WindowFocused(window);

		// Then
		Assert.Equal(window, workspace.LastFocusedWindow);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void WindowFocused_DoesNotContainWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given the window is not in the workspace
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When
		workspace.WindowFocused(window);

		// Then
		Assert.Null(workspace.LastFocusedWindow);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void WindowFocused_WindowIsNull(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given the window is null
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When
		workspace.WindowFocused(window);
		workspace.WindowFocused(null);

		// Then
		Assert.Null(workspace.LastFocusedWindow);
	}
	#endregion



	#region FocusLastFocusedWindow
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void FocusLastFocusedWindow_LastFocusedWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given the window is in the workspace
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);

		window.ClearReceivedCalls();

		// When FocusLastFocusedWindow is called
		workspace.WindowFocused(window);
		workspace.FocusLastFocusedWindow();

		// Then the LayoutEngine's GetFirstWindow method is called
		layoutEngine.DidNotReceive().GetFirstWindow();
		window.Received(1).Focus();
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void FocusLastFocusedWindow_EmptyWorkspace_MonitorNotFound(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given there are no windows in the workspace, and the workspace is not on a monitor
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ctx.Butler.GetMonitorForWorkspace(workspace).Returns((IMonitor?)null);

		// When FocusLastFocusedWindow is called
		workspace.FocusLastFocusedWindow();

		// Then
		internalCtx.MonitorManager.DidNotReceive().ActivateEmptyMonitor(Arg.Any<IMonitor>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void FocusLastFocusedWindow_EmptyWorkspace_MonitorFound(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IMonitor monitor
	)
	{
		// Given there are no windows in the workspace, and the workspace is on a monitor
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ctx.Butler.GetMonitorForWorkspace(workspace).Returns(monitor);

		// When FocusLastFocusedWindow is called
		workspace.FocusLastFocusedWindow();

		// Then
		internalCtx.MonitorManager.Received(1).ActivateEmptyMonitor(monitor);
	}
	#endregion

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void NextLayoutEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When NextLayoutEngine is called
		workspace.CycleLayoutEngine(false);

		// Then the active layout engine is set to the next one
		Assert.True(Object.ReferenceEquals(layoutEngine2, workspace.ActiveLayoutEngine));
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void NextLayoutEngine_LastEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When NextLayoutEngine is called
		workspace.CycleLayoutEngine(false);
		workspace.CycleLayoutEngine(false);

		// Then the active layout engine is set to the first one
		Assert.True(Object.ReferenceEquals(layoutEngine, workspace.ActiveLayoutEngine));
		Assert.Same(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PreviousLayoutEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When PreviousLayoutEngine is called
		workspace.CycleLayoutEngine(true);

		// Then the active layout engine is set to the previous one
		Assert.True(Object.ReferenceEquals(layoutEngine2, workspace.ActiveLayoutEngine));
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void ActivatePreviouslyActiveLayoutEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine0,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(
				ctx,
				internalCtx,
				triggers,
				"Workspace",
				new ILayoutEngine[] { layoutEngine0, layoutEngine1, layoutEngine2 }
			);

		// When PreviousLayoutEngine is called
		workspace.CycleLayoutEngine();
		workspace.CycleLayoutEngine();
		workspace.ActivatePreviouslyActiveLayoutEngine();

		// Then the active layout engine is set to the first one
		Assert.Same(layoutEngine1, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PreviousLayoutEngine_FirstEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine2
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine2 });
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When PreviousLayoutEngine is called
		workspace.CycleLayoutEngine(true);
		workspace.CycleLayoutEngine(true);

		// Then the active layout engine is set to the last one
		Assert.True(Object.ReferenceEquals(layoutEngine, workspace.ActiveLayoutEngine));
		Assert.Same(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void AddWindow_Fails_AlreadyIncludesWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When AddWindow is called
		workspace.AddWindow(window);
		workspace.AddWindow(window);

		// Then the window is added to the layout engine
		layoutEngine.Received(1).AddWindow(window);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void AddWindow_Success(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When AddWindow is called
		workspace.AddWindow(window);

		// Then the window is added to the layout engine
		layoutEngine1.Received(1).AddWindow(window);
		layoutEngine2.Received(1).AddWindow(window);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	#region RemoveWindow
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void RemoveWindow_Fails_AlreadyRemoved(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When RemoveWindow is called
		workspace.RemoveWindow(window);
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		bool result = workspace.RemoveWindow(window);

		// Then the window is removed from the layout engine
		Assert.False(result);
		layoutEngine.DidNotReceive().RemoveWindow(window);
		internalCtx.CoreNativeManager.DidNotReceive().IsWindow(Arg.Any<HWND>());
		Assert.Same(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void RemoveWindow_Fails_DidNotRemoveFromLayoutEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);

		internalCtx.CoreNativeManager.ClearReceivedCalls();

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		givenEngine.RemoveWindow(window).Returns(givenEngine);

		// When RemoveWindow is called
		bool result = workspace.RemoveWindow(window);

		// Then the window is removed from the layout engine
		Assert.False(result);
		givenEngine.Received(1).RemoveWindow(window);
		internalCtx.CoreNativeManager.DidNotReceive().IsWindow(Arg.Any<HWND>());
		Assert.Null(workspace.LastFocusedWindow);
		Assert.Same(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void RemoveWindow_Success_OneWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given only one window is in the workspace
		layoutEngine2.AddWindow(window).Returns(layoutEngine2);

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });
		workspace.AddWindow(window);
		workspace.WindowFocused(window);

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		ctx.Butler.ClearReceivedCalls();
		internalCtx.CoreNativeManager.ClearReceivedCalls();

		// When RemoveWindow is called
		bool result = workspace.RemoveWindow(window);

		// Then the window is removed from the layout engine, and LastFocusedWindow is set to null
		Assert.True(result);
		givenEngine.Received(1).RemoveWindow(window);
		layoutEngine2.Received(1).RemoveWindow(window);

		Assert.Null(workspace.LastFocusedWindow);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void RemoveWindow_Success_MultipleWindows(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window,
		IWindow window2,
		IWindow window3
	)
	{
		// Given multiple windows are in the workspace
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		workspace.AddWindow(window2);
		workspace.AddWindow(window3);
		workspace.WindowFocused(window);

		window2.IsMinimized.Returns(true);

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		ctx.Butler.ClearReceivedCalls();
		internalCtx.CoreNativeManager.ClearReceivedCalls();

		// When RemoveWindow is called
		bool result = workspace.RemoveWindow(window);

		// Then the window is removed from the layout engine, and LastFocusedWindow is set to the next window
		Assert.True(result);
		givenEngine.Received(1).RemoveWindow(window);
		Assert.Equal(window3, workspace.LastFocusedWindow);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}
	#endregion

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void FocusWindowInDirection_Fails_DoesNotContainWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When FocusWindowInDirection is called
		bool result = workspace.FocusWindowInDirection(Direction.Up, window);

		// Then the layout engine is not told to focus the window
		Assert.False(result);
		layoutEngine.DidNotReceive().FocusWindowInDirection(Direction.Up, window);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory]
	[InlineAutoSubstituteData<WorkspaceCustomization>(false, 1)]
	[InlineAutoSubstituteData<WorkspaceCustomization>(true, 0)]
	internal void FocusWindowInDirection_Success(
		bool deferLayout,
		int doLayoutCalls,
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When FocusWindowInDirection is called
		bool result = workspace.FocusWindowInDirection(Direction.Up, window, deferLayout);

		// Then the layout engine is told to focus the window, and a layout occurs
		Assert.True(result);
		activeLayoutEngine.Received(1).FocusWindowInDirection(Direction.Up, window);
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.Received(doLayoutCalls)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void FocusWindowInDirection_NoLayoutEngineChange(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given the active layout engine does not change when calling FocusWindowInDirection
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });
		workspace.AddWindow(window);

		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;
		activeLayoutEngine.FocusWindowInDirection(Direction.Up, window).Returns(activeLayoutEngine);
		activeLayoutEngine.ClearReceivedCalls();

		// When FocusWindowInDirection is called
		workspace.FocusWindowInDirection(Direction.Up, window);

		// Then the layout engine is told to focus the window, and no layout occurs
		activeLayoutEngine.Received(1).FocusWindowInDirection(Direction.Up, window);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		activeLayoutEngine.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());
	}

	#region SwapWindowInDirection
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void SwapWindowInDirection_Fails_WindowIsNull(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When SwapWindowInDirection is called
		bool result = workspace.SwapWindowInDirection(Direction.Up, null);

		// Then the layout engine is not told to swap the window
		Assert.False(result);
		layoutEngine.DidNotReceive().SwapWindowInDirection(Direction.Up, Arg.Any<IWindow>());
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void SwapWindowInDirection_Fails_DoesNotContainWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When SwapWindowInDirection is called
		bool result = workspace.SwapWindowInDirection(Direction.Up, window);

		// Then the layout engine is not told to swap the window
		Assert.False(result);
		layoutEngine.DidNotReceive().SwapWindowInDirection(Direction.Up, window);
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	[Theory]
	[InlineAutoSubstituteData<WorkspaceCustomization>(false, 1)]
	[InlineAutoSubstituteData<WorkspaceCustomization>(true, 0)]
	internal void SwapWindowInDirection_Success(
		bool deferLayout,
		int doLayoutCalls,
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When SwapWindowInDirection is called
		bool result = workspace.SwapWindowInDirection(Direction.Up, window, deferLayout);

		// Then the layout engine is told to swap the window
		Assert.True(result);
		givenEngine.Received(1).SwapWindowInDirection(Direction.Up, window);
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
		internalCtx
			.DeferWorkspacePosManager.Received(doLayoutCalls)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}
	#endregion


	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void MoveWindowEdgesInDirection_Fails_WindowIsNull(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		IPoint<double> deltas = new Point<double>() { X = 0.3, Y = 0 };
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When MoveWindowEdgesInDirection is called
		bool result = workspace.MoveWindowEdgesInDirection(Direction.Up, deltas, null);

		// Then the layout engine is not told to move the window
		Assert.False(result);
		layoutEngine.DidNotReceive().MoveWindowEdgesInDirection(Direction.Up, deltas, Arg.Any<IWindow>());
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void MoveWindowEdgesInDirection_Fails_DoesNotContainWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		IPoint<double> deltas = new Point<double>() { X = 0.3, Y = 0 };
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When MoveWindowEdgesInDirection is called
		bool result = workspace.MoveWindowEdgesInDirection(Direction.Up, deltas, window);

		// Then the layout engine is not told to move the window
		Assert.False(result);
		layoutEngine.DidNotReceive().MoveWindowEdgesInDirection(Direction.Up, deltas, window);
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
		Assert.Same(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory]
	[InlineAutoSubstituteData<WorkspaceCustomization>(false, 1)]
	[InlineAutoSubstituteData<WorkspaceCustomization>(true, 0)]
	internal void MoveWindowEdgesInDirection_Success(
		bool deferLayout,
		int doLayoutCalls,
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		IPoint<double> deltas = new Point<double>() { X = 0.3, Y = 0 };
		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When MoveWindowEdgesInDirection is called
		bool result = workspace.MoveWindowEdgesInDirection(Direction.Up, deltas, window, deferLayout);

		// Then the layout engine is told to move the window
		Assert.True(result);
		givenEngine.Received(1).MoveWindowEdgesInDirection(Direction.Up, deltas, window);
		internalCtx
			.DeferWorkspacePosManager.Received(doLayoutCalls)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory]
	[InlineAutoSubstituteData<WorkspaceCustomization>(false, 1)]
	[InlineAutoSubstituteData<WorkspaceCustomization>(true, 0)]
	internal void MoveWindowToPoint_Success_AddWindow(
		bool deferLayout,
		int doLayoutCalls,
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		IPoint<double> point = new Point<double>() { X = 0.3, Y = 0.3 };
		window.ClearReceivedCalls();
		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When MoveWindowToPoint is called
		bool result = workspace.MoveWindowToPoint(window, point, deferLayout);

		// Then the layout engine is told to move the window
		Assert.True(result);
		layoutEngine.Received(1).MoveWindowToPoint(window, point);
		layoutEngine.DidNotReceive().RemoveWindow(window);
		internalCtx
			.DeferWorkspacePosManager.Received(doLayoutCalls)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory]
	[InlineAutoSubstituteData<WorkspaceCustomization>(false, 1)]
	[InlineAutoSubstituteData<WorkspaceCustomization>(true, 0)]
	internal void MoveWindowToPoint_Success_WindowAlreadyExists(
		bool deferLayout,
		int doLayoutCalls,
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		IPoint<double> point = new Point<double>() { X = 0.3, Y = 0.3 };

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;
		window.ClearReceivedCalls();

		ILayoutEngine activeLayoutEngine = workspace.ActiveLayoutEngine;

		// When MoveWindowToPoint is called
		bool result = workspace.MoveWindowToPoint(window, point, deferLayout);

		// Then the layout engine is told to remove and add the window
		Assert.True(result);
		givenEngine.Received(1).MoveWindowToPoint(window, point);
		internalCtx
			.DeferWorkspacePosManager.Received(doLayoutCalls)
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
		Assert.NotSame(activeLayoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void ToString_Success(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		// When ToString is called
		string result = workspace.ToString();

		// Then the result is as expected
		Assert.Equal("Workspace", result);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void Deactivate(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window,
		IWindow window2
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);
		workspace.AddWindow(window2);
		internalCtx.CoreNativeManager.ClearReceivedCalls();

		// When Deactivate is called
		workspace.Deactivate();

		// Then the windows are hidden and DoLayout is called
		internalCtx.CoreNativeManager.DidNotReceive().IsWindow(Arg.Any<HWND>());
		window.Received(1).Hide();
		window2.Received(1).Hide();
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void TryGetWindowState(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		IWindow window,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		IWindowState windowState = new WindowState()
		{
			Rectangle = new Rectangle<int>(),
			Window = window,
			WindowSize = WindowSize.Normal
		};

		Workspace sut = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		internalCtx
			.DeferWorkspacePosManager.WhenForAnyArgs(x =>
				x.DoLayout(
					Arg.Any<IWorkspace>(),
					Arg.Any<WorkspaceManagerTriggers>(),
					Arg.Any<Dictionary<HWND, IWindowState>>()
				)
			)
			.Do(x =>
			{
				// Populate the dictionary with the window state
				Dictionary<HWND, IWindowState> dict = x.ArgAt<Dictionary<HWND, IWindowState>>(2);
				dict.Add(window.Handle, windowState);
			});

		// When TryGetWindowState is called after adding a window and triggering a layout
		sut.AddWindow(window);
		sut.DoLayout();
		IWindowState? result = sut.TryGetWindowState(window);

		// Then the result is as expected
		Assert.NotNull(result);
		Assert.Equal(windowState, result);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void Dispose(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		IWindow window,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		ctx.Butler.GetMonitorForWorkspace(Arg.Any<IWorkspace>()).Returns((IMonitor?)null);

		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.AddWindow(window);

		// When Dispose is called
		workspace.Dispose();

		// Then the window is minimized
		window.Received(1).ShowMinimized();
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void MinimizeWindowStart_ContainsWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given the workspace doesn't contain the window
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });
		workspace.AddWindow(window);

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When MinimizeWindowStart is called
		workspace.MinimizeWindowStart(window);

		// Then
		givenEngine.Received(1).MinimizeWindowStart(window);
		layoutEngine2.DidNotReceive().MinimizeWindowStart(window);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void MinimizeWindowStart_DoesNotContainWindow(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given
		layoutEngine2.AddWindow(window).Returns(layoutEngine2);

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		// When MinimizeWindowStart is called
		workspace.MinimizeWindowStart(window);

		// Then
		givenEngine.Received(1).MinimizeWindowStart(window);
		layoutEngine2.Received(1).MinimizeWindowStart(window);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void MinimizeWindowEnd(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine2,
		IWindow window
	)
	{
		// Given
		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine1, layoutEngine2 });
		workspace.AddWindow(window);
		workspace.MinimizeWindowStart(window);

		ILayoutEngine givenEngine = workspace.ActiveLayoutEngine;

		window.ClearReceivedCalls();

		// When MinimizeWindowEnd is called
		workspace.MinimizeWindowEnd(window);

		// Then
		givenEngine.Received(1).MinimizeWindowEnd(window);
		layoutEngine2.DidNotReceive().MinimizeWindowEnd(window);
		Assert.NotSame(givenEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void DoLayout_Disposed(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine
	)
	{
		// Given
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });
		workspace.Dispose();

		// When DoLayout is called
		workspace.DoLayout();

		// Then
		internalCtx
			.DeferWorkspacePosManager.DidNotReceive()
			.DoLayout(workspace, Arg.Any<WorkspaceManagerTriggers>(), Arg.Any<Dictionary<HWND, IWindowState>>());
	}

	#region PerformCustomLayoutEngineAction
	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PerformCustomLayoutEngineAction_PassWindowThroughAsArgs(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		layoutEngine.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<IWindow?>>()).Returns(layoutEngine);
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		LayoutEngineCustomAction action = new() { Name = "Action", Window = window };

		layoutEngine.ClearReceivedCalls();

		// When PerformCustomLayoutEngineAction is called
		workspace.PerformCustomLayoutEngineAction(action);

		// Then the layout engine is not changed
		layoutEngine.Received(1).PerformCustomAction(Arg.Any<LayoutEngineCustomAction<IWindow?>>());
		layoutEngine.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());
		Assert.Same(layoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PerformCustomLayoutEngineAction_NoChange(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		IWindow window
	)
	{
		// Given
		layoutEngine.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<string>>()).Returns(layoutEngine);
		Workspace workspace = new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine });

		LayoutEngineCustomAction<string> action =
			new()
			{
				Name = "Action",
				Payload = "payload",
				Window = window
			};

		layoutEngine.ClearReceivedCalls();

		// When PerformCustomLayoutEngineAction is called
		workspace.PerformCustomLayoutEngineAction(action);

		// Then the layout engine is not changed
		layoutEngine.Received(1).PerformCustomAction(action);
		layoutEngine.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());
		Assert.Same(layoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PerformCustomLayoutEngineAction_ChangeInInactiveEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngine1,
		ILayoutEngine layoutEngine1Result,
		IWindow window
	)
	{
		// Given
		layoutEngine.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<string>>()).Returns(layoutEngine);
		layoutEngine1.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<string>>()).Returns(layoutEngine1Result);

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine1 });

		LayoutEngineCustomAction<string> action =
			new()
			{
				Name = "Action",
				Payload = "payload",
				Window = window
			};

		layoutEngine.ClearReceivedCalls();

		// When PerformCustomLayoutEngineAction is called
		workspace.PerformCustomLayoutEngineAction(action);

		// Then the layout engine is changed
		layoutEngine.Received(1).PerformCustomAction(action);
		layoutEngine.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());

		layoutEngine1.Received(1).PerformCustomAction(action);
		layoutEngine1.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());
		layoutEngine1Result.DidNotReceive().DoLayout(Arg.Any<IRectangle<int>>(), Arg.Any<IMonitor>());

		Assert.Same(layoutEngine, workspace.ActiveLayoutEngine);
	}

	[Theory, AutoSubstituteData<WorkspaceCustomization>]
	internal void PerformCustomLayoutEngineAction_ChangeInActiveEngine(
		IContext ctx,
		IInternalContext internalCtx,
		WorkspaceManagerTriggers triggers,
		ILayoutEngine layoutEngine,
		ILayoutEngine layoutEngineResult,
		ILayoutEngine layoutEngine1,
		IWindow window
	)
	{
		// Given
		layoutEngine.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<string>>()).Returns(layoutEngineResult);
		layoutEngine1.PerformCustomAction(Arg.Any<LayoutEngineCustomAction<string>>()).Returns(layoutEngine1);

		Workspace workspace =
			new(ctx, internalCtx, triggers, "Workspace", new ILayoutEngine[] { layoutEngine, layoutEngine1 });

		LayoutEngineCustomAction<string> action =
			new()
			{
				Name = "Action",
				Payload = "payload",
				Window = window
			};

		layoutEngine.ClearReceivedCalls();

		// When PerformCustomLayoutEngineAction is called
		workspace.PerformCustomLayoutEngineAction(action);

		// Then the layout engine is changed
		layoutEngine.Received(1).PerformCustomAction(action);
		Assert.Same(layoutEngineResult, workspace.ActiveLayoutEngine);
	}
	#endregion
}
