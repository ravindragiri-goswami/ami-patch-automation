namespace AMI.Patch.Automation.Console.Models
{
    public class GoldenAMIPatchModel
    {
        public string AutoScalingGroupName { get; set; }
        public int DesiredCapacity { get; set; }
        public string PatchDocumentName { get; set; }
        public string GoldenImageAmiId { get; set; }
        public string LaunchConfigurationName { get; set; }
    }
}
