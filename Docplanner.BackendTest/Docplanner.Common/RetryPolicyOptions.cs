public class RetryPolicyOptions
{
    public int RetryCount { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 2;
}
