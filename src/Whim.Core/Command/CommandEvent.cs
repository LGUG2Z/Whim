using System;

namespace Whim.Core;

public delegate void CommandEventHandler(object sender, CommandEventArgs args);

public class CommandEventArgs : EventArgs
{
	public ICommand Command { get; set; }

	public CommandEventArgs(ICommand command)
	{
		Command = command;
	}
}
