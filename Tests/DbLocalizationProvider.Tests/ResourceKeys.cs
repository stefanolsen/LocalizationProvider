namespace DbLocalizationProvider.Tests
{
    [LocalizedResource]
    public class PageResources
    {
        public static HeaderResources Header { get; }

        public class HeaderResources
        {
            public string HelloMessage => "This is default value for resource";
        }
    }

    [LocalizedResource]
    public class ResourceKeys
    {
        public ResourceKeys()
        {
            SubResource = new SubResourceKeys();
        }

        public string SampleResource { get; set; }

        public static SubResourceKeys SubResource { get; set; }

        public static string ThisIsConstant => "Default value for constant";
    }

    public class SubResourceKeys
    {
        public SubResourceKeys()
        {
            EvenMoreComplexResource = new DeeperSubResourceModel();
        }

        public string SubResourceProperty => "Sub Resource Property";

        public int AnotherResource { get; set; }

        public DeeperSubResourceModel EvenMoreComplexResource { get; set; }
    }

    public class DeeperSubResourceModel
    {
        public decimal Amount { get; set; }
    }
}
