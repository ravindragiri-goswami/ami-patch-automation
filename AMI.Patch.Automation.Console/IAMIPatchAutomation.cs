using AMI.Patch.Automation.Console.Models;

namespace AMI.Patch.Automation.Console
{
    public interface IAMIPatchAutomation
    {
        Task Execute(BaseInputModel baseInput, AMIPatchModel amiPatch);
    }
}
