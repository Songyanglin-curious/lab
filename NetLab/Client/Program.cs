using NetLab.Client;

// Phase 5：加密测试
new EchoClient("127.0.0.1", 9000).RunSecureTest();

// 其他可用测试：
// new EchoClient("127.0.0.1", 9000).RunTest();
// new EchoClient("127.0.0.1", 9000).RunPooledTest();
// new EchoClient("127.0.0.1", 9000).RunDispatchTest();
