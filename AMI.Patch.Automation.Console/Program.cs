using AMI.Patch.Automation.Console;
using AMI.Patch.Automation.Console.Models;

namespace AwsSdkAutomation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            BaseInputModel baseInputModel = new BaseInputModel()
            {
                AccessKey = "YOUR_ACCESS_KEY",
                SecretKey = "YOUR_SECRET_KEY",
                Region = "us-west-2"
            };

            GoldenAMIPatchModel goldenAMIPatchModel = new GoldenAMIPatchModel()
            {
                AutoScalingGroupName = "my-auto-scaling-group", // your Auto Scaling Group name
                DesiredCapacity = 3, // Desired capacity of new instances
                PatchDocumentName = "MyPatchDocument", // patch document name
                GoldenImageAmiId = "ami-12345678", // Golden Image AMI ID
                LaunchConfigurationName = "YOUR_LAUNCH_CONFIGURATION_NAME" // Launch Configuration name
            };

            try
            {
                GoldenAMIPatchAutomation goldenAMIPatchAutomation = new GoldenAMIPatchAutomation();
                await goldenAMIPatchAutomation.Execute(baseInputModel, goldenAMIPatchModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex}");
            }
        }
    }
}

