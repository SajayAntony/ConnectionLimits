using CmdLine;

namespace Connect
{

    [CommandLineArguments(Program = "Connect", Title = "Connection Limit Test", Description = "Test for connection limit")]
    class ConnectArgs
    {
        [CommandLineParameter(Command = "?", Default = false, Description = "Show Help", Name = "Help", IsHelp = true)]
        public bool Help { get; set; }

        [CommandLineParameter(Command = "mode", ParameterIndex = 1, Required = false, Description = "Specified either client mode or server model")]
        public string mode { get; set; }

        [CommandLineParameter(Command = "server", Required = false, Description = "Server to connect to")]
        public string server { get; set; }


        [CommandLineParameter(Command = "port", Required = false, Default = 8080, Description = "Server port to listen or connect to.")]
        public int port { get; set; }

        [CommandLineParameter(Command = "climit", Required = false, Default = 1, Description = "Number of connection.")]
        public int connectionLimit { get; set; }

        [CommandLineParameter(Command = "rate", Required = false, Default = 1, Description = "Rate of outbound messages.")]
        public int messageRate { get; set; }
    }
}
