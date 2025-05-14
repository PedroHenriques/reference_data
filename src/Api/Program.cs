using System.Dynamic;
using StackExchange.Redis;
using Confluent.Kafka;
using Toolkit;
using Toolkit.Types;
using MongodbUtils = Toolkit.Utils.Mongodb;
using RedisUtils = Toolkit.Utils.Redis;
using KafkaUtils = Toolkit.Utils.Kafka<string, dynamic>;
using Confluent.SchemaRegistry;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.MapGet(
  "/",
  async () =>
  {
    dynamic document = new ExpandoObject();
    document.prop1 = "value 1";
    document.prop2 = "value 2";

    string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
    if (mongoConStr == null)
    {
      throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
    }
    MongoDbInputs mongodbInputs = MongodbUtils.PrepareInputs(mongoConStr);
    IMongodb mongoDb = new Mongodb(mongodbInputs);
    await mongoDb.InsertOne<dynamic>("myTestDb", "myTestCol", document);


    string? redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR");
    if (redisConStr == null)
    {
      throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");
    }
    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { redisConStr },
    };
    RedisInputs redisInputs = RedisUtils.PrepareInputs(redisConOpts);
    ICache redis = new Redis(redisInputs);
    await redis.Set("prop1", document.prop1);
    await redis.Set("prop2", document.prop2);


    string? schemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
    if (schemaRegistryUrl == null)
    {
      throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
    }
    SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };

    string? kafkaConStr = Environment.GetEnvironmentVariable("KAFKA_CON_STR");
    if (kafkaConStr == null)
    {
      throw new Exception("Could not get the 'KAFKA_CON_STR' environment variable");
    }
    var producerConfig = new ProducerConfig
    {
      BootstrapServers = kafkaConStr,
    };

    KafkaInputs<string, dynamic> kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, "myTestTopic-value", 1, producerConfig
    );
    IKafka<string, dynamic> kafka = new Kafka<string, dynamic>(kafkaInputs);
    kafka.Publish(
      "myTestTopic",
      new Message<string, dynamic> { Key = "prop1", Value = document },
      (res) => { Console.WriteLine($"Event inserted in partition: {res.Partition} and offset: {res.Offset}."); }
    );


    return Results.Ok("Hello World!");
  }
);

app.Run();
