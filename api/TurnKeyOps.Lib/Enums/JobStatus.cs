namespace TurnKeyOps.Lib.Enums;

public enum JobStatus
{
    Created = 0,
    Scheduled = 1,
    InProgress = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5,
    Closed = 6,
    Lead = 7,
    Estimated = 8,
    Invoiced = 9,
    Paid = 10,
    Draft = 11, Planning = 12, Blocked = 13, ReadyToSchedule = 14, ReadyToStart = 15,
    Paused = 16, Waiting = 17, ReadyForCompletion = 18, CompletionReview = 19
}
