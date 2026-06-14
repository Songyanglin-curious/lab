using NetLab.Protocol;
using NetLab.Protocol.Channels;

BusinessServer server = new BusinessServer(9000);

server.RegisterProcess(Business.Manage, () => new EchoChannel());
server.RegisterProcess(Business.File, () => new FileChannel());
server.RegisterProcess(Business.Agent, () => new AgentChannel());

server.Start();
