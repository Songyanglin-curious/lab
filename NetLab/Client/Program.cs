using NetLab.Client;

// Phase 4：业务分发演示（RunDispatchTest）
new EchoClient("127.0.0.1", 9000).RunDispatchTest();

// 其他可用测试：
// new EchoClient("127.0.0.1", 9000).RunTest();        // 长连接 echo
// new EchoClient("127.0.0.1", 9000).RunPooledTest();   // 连接池复用验证
