using NetLab.Protocol;
using NetLab.Protocol.Channels;
using NetLab.Protocol.Exchange;

BusinessServer server = new BusinessServer(9000);

// Phase 5：开启加密（注释掉即恢复明文）
server.ExchangeWrapper = ex => new SecureExchange(ex);

server.RegisterProcess(Business.Manage, () => new EchoChannel());
server.RegisterProcess(Business.File, () => new FileChannel());
server.RegisterProcess(Business.Agent, () => new AgentChannel());

server.Start();
