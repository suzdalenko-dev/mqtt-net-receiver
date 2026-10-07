### mqtt-net-service

Our itinerary should be:
1. C# y .NET fundamentales: tipos, clases, interfaces, generics, namespaces, assemblies, using, exceptions, nullable.
2. Generic Host: Program.cs, DI, configuración, logging, lifecycle.
3. Async: Task, async, await, ThreadPool, CancellationToken, diferencia Task/Thread.
4. Worker Service: BackgroundService, varios workers, errores críticos, shutdown.
5. MQTTnet: conexión, callback, reconexión, suscripción.
6. Models/records: crear nuestro MqttMessage.
7. Channel<T>: productor/consumidor como tus queues Python.
8. Files: escritura async del log.
9. Npgsql: pooling, connections, commands, parameters, transactions.
10. DI lifetimes: Singleton, Scoped, Transient.
11. ASP.NET Core: cuando ya controles lo anterior, API, routing, middleware, controllers, Minimal APIs.
12. EF Core: DbContext, entidades, LINQ, migrations.
13. Deployment: Docker, systemd, Windows Service, publishing.
14. Testing: xUnit, mocking, integration tests.





docker compose up -d --build
docker compose logs --tail 50 -f mqtt

StopAsync
