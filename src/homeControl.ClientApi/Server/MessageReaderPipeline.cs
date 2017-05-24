using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using homeControl.ClientServerShared;

namespace homeControl.ClientApi.Server
{
    internal sealed class MessageReaderPipeline
    {
        private readonly IClientReader _reader;
        private readonly IClientMessageSerializer _serializer;
        private readonly BufferBlock<byte[]> _input;
        private readonly CancellationToken _ct;

        public MessageReaderPipeline(IClientReader reader, IClientMessageSerializer serializer, CancellationToken ct)
        {
            Guard.DebugAssertArgumentNotNull(reader, nameof(reader));
            Guard.DebugAssertArgumentNotNull(serializer, nameof(serializer));

            _reader = reader;
            _serializer = serializer;

            _ct = ct;
            _input = CreatePipeline();
        }

        private BufferBlock<byte[]> CreatePipeline()
        {
            var bufferOptions = new DataflowBlockOptions
            {
                EnsureOrdered = true,
                CancellationToken = _ct
            };
            var executionOptions = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _ct,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1,
                SingleProducerConstrained = true
            };

            var input = new BufferBlock<byte[]>(bufferOptions);
            var deserializer = new TransformBlock<byte[], IClientMessage>(msg => DeserializeMessage(msg), executionOptions);
            var output = new ActionBlock<IClientMessage>(msg => OnMessageReceived(msg), executionOptions);

            var linkOpts = new DataflowLinkOptions {PropagateCompletion = true};
            input.LinkTo(deserializer, linkOpts);
            deserializer.LinkTo(output, linkOpts);

            Task.Factory.StartNew(ReaderLoop, _ct);

            return input;
        }

        private IClientMessage DeserializeMessage(byte[] msgBytes)
        {
            Guard.DebugAssertArgumentNotNull(msgBytes, nameof(msgBytes));
            return _serializer.Deserialize(msgBytes);
        }

        private async Task<byte[]> ReadBytes(int count)
        {
            Guard.DebugAssertArgument(count > 0, nameof(count));

            var buffer = new byte[count];
            var position = 0;
            while (position < count && !_ct.IsCancellationRequested)
            {
                position += await _reader.ReceiveDataAsync(buffer, position, count-position, _ct);
            }

            return buffer;
        }

        private async void ReaderLoop()
        {
            while (!_ct.IsCancellationRequested)
            {
                var lengthBytes = await ReadBytes(sizeof(int));
                var length = BitConverter.ToInt32(lengthBytes, 0);

                var messageBytes = length > 0 ? await ReadBytes(length) : new byte[0];
                await _input.SendAsync(messageBytes, _ct);
            }
        }

        public event EventHandler<IClientMessage> MessageReceived;
        private void OnMessageReceived(IClientMessage msg)
        {
            Guard.DebugAssertArgumentNotNull(msg, nameof(msg));

            var handler = Interlocked.CompareExchange(ref MessageReceived, null, null);
            handler?.Invoke(this, msg);
        }
    }
}