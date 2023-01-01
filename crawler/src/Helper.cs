using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace tbm.Crawler
{
    public abstract class Helper
    {
        public static byte[]? SerializedProtoBufOrNullIfEmpty(IMessage? protoBuf) =>
            protoBuf == null || protoBuf.CalculateSize() == 0 ? null : protoBuf.ToByteArray();

        public static byte[]? SerializedProtoBufWrapperOrNullIfEmpty
            <T>(RepeatedField<T>? valuesToWrap, Func<IMessage> wrapperFactory) where T : IMessage =>
            valuesToWrap?.Select(i => i.CalculateSize()).Sum() is 0 or null
                ? null
                : SerializedProtoBufOrNullIfEmpty(wrapperFactory());

        private static readonly JsonSerializerOptions UnescapedSerializeOptions =
            new() {Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)};

        public static string UnescapedJsonSerialize<TValue>(TValue value) =>
            JsonSerializer.Serialize(value, UnescapedSerializeOptions);
    }
}
