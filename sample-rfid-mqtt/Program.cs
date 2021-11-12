using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sample_rfid_mqtt
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("Usage:  sample-rfid-mqtt serveraddress [port]");
                return;
            }

            // Create a new MQTT client.
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            // Get the server address and port from the command line arguments.
            var serverAddress = args[0];
            var port = 1883;
            if (args.Length == 2)
            {
                port = int.Parse(args[1]);
            }

            // Create TCP based options using the builder.
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(serverAddress, port)
                .Build();

            // Determine the MQTT topic to subscribe to.
            // + is a wildcard in MQTT topic names.
            var subscriptionTopic = "+/get/rfidtag/+";      // replace the first plus with a single BAC's name, if desired.

            // Reconnect to the server if we get disconnected.
            mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(async (e) =>
            {
                Console.WriteLine("### DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    await mqttClient.ConnectAsync(options, CancellationToken.None);
                    await mqttClient.SubscribeAsync(subscriptionTopic);
                }
                catch
                {
                    Console.WriteLine("### RECONNECTING FAILED ###");
                }
            });

            // Print out a helpful message when we are connected to the server.
            mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
            {
                Console.WriteLine("Connected successfully.");
            });

            // Handle incoming messages with RFID data.
            mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(async (e) =>
            {
                // Convert the byte buffer of the message to a string.
                var messageText = e.ApplicationMessage.ConvertPayloadToString();
                
                // Log the message before we try to make sense out of it.
                Console.WriteLine("Raw message received on topic {0}: {1}", e.ApplicationMessage.Topic, messageText);

                // Split the topic name into its component pieces with slashes, creating a four element array.
                var tokens = e.ApplicationMessage.Topic.Split("/");
                if (tokens.Length != 4)
                {
                    Console.WriteLine("Unexpected topic in subscription.");
                    return;
                }

                // Extract the BAC name from the first token and the reader number from the fourth.
                var bacName = tokens[0];
                int readerNumber = -1;
                if (!int.TryParse(tokens[3], out readerNumber))
                {
                    Console.WriteLine("Couldn't determine the reader number from the topic.");
                    return;
                }

                // If no tag is present, the message will be "NONE" - otherwise it will be the tag ID.
                var tagPresent = String.CompareOrdinal(messageText, "NONE") != 0;
                var tagId = tagPresent ? messageText : String.Empty;

                // Output the data.
                Console.WriteLine("BAC {0}, Reader {1}:  Tag is {2}.", bacName, readerNumber, tagPresent ? tagId : "not present.");

            });

            // Connect to the server and subscribe to the topic.
            await mqttClient.ConnectAsync(options, CancellationToken.None);
            await mqttClient.SubscribeAsync(subscriptionTopic);

            // Keep running until the user presses a key to exit.
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
