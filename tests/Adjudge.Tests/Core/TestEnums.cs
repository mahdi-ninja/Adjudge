namespace Adjudge.Tests.Core;

public enum Intent
{
    [Option("Charges, invoices, refunds", NotFor = "Order tracking", Examples = ["I was charged twice"])]
    Billing,

    [Option("Where an existing order is")]
    Tracking,

    Returns,
}

public enum Urgency
{
    [Level("Can wait days")]
    Low,

    Medium,

    [Level("Needs attention now")]
    High,
}

public enum Priority
{
    [Option("Can wait")]
    Low = 30,

    [Option("Jump the queue")]
    High = 10,
}

public enum Solo
{
    Only,
}

public enum Eleven
{
    A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10,
}

public enum Blank
{
}
