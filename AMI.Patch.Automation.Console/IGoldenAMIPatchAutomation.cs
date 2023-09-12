using AMI.Patch.Automation.Console.Models;

namespace AMI.Patch.Automation.Console
{
    public interface IGoldenAMIPatchAutomation
    {
        Task Execute(BaseInputModel baseInput, GoldenAMIPatchModel goldenAMIPatch);
    }
}
