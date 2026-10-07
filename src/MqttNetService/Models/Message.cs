public sealed record Message
(
    DateTimeOffset DateUtc,
    DateTimeOffset DateLocal,
    string Topic,
    string Content
);