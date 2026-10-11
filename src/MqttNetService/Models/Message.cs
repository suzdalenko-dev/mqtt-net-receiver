public sealed record Message
(
    // utc date
    DateTime DateUtc,
    // local date
    DateTime DateLocal,
    string Topic,
    string Content
);