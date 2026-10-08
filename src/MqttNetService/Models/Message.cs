public sealed record Message
(
    // utc date
    DateTime dateUtc,
    // local date
    DateTime dateLocal,
    string topic,
    string content
);