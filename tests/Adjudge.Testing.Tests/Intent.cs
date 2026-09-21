namespace Adjudge.Testing.Tests;

public enum Intent
{
    [Option("Charges, invoices, refunds")]
    Billing,

    [Option("Where an existing order is")]
    Tracking,

    Returns,
}
