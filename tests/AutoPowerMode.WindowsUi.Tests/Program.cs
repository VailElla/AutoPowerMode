using AutoPowerMode;
using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode.WindowsUi.Tests;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            VerifySettingsLayout(AppLanguagePreference.SimplifiedChinese);
            VerifySettingsLayout(AppLanguagePreference.English);

            Console.WriteLine("PASS SettingsForm opens and resizes without horizontal scrolling.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL SettingsForm Windows UI smoke test.");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifySettingsLayout(string languagePreference)
    {
        LocalizationService.UsePreference(languagePreference);

        var plans = new[]
        {
            new PowerPlan
            {
                Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                Name = "High Performance",
                IsActive = true
            },
            new PowerPlan
            {
                Guid = "a1841308-3541-4fab-bc81-f71556f20b4a",
                Name = "Power Saver"
            }
        };
        var config = new AppConfig
        {
            IdleThresholdSeconds = 1000,
            ActiveCheckIntervalSeconds = 30,
            IdleCheckIntervalSeconds = 1,
            ActivePowerPlanGuid = plans[0].Guid,
            IdlePowerPlanGuid = plans[1].Guid,
            NotificationsEnabled = true,
            Language = languagePreference,
            PowerPlansConfiguredByUser = true
        };

        using var form = new SettingsForm(
            config,
            plans,
            LocalizationService.Text("StatusWaiting"))
        {
            ShowInTaskbar = false
        };

        form.Show();
        ProcessPendingLayout(form);

        foreach (var clientSize in new[]
                 {
                     new Size(700, 520),
                     new Size(400, 240),
                     new Size(460, 270),
                     new Size(900, 600)
                 })
        {
            form.ClientSize = clientSize;
            ProcessPendingLayout(form);

            var scrollPanel = Descendants(form)
                .OfType<Panel>()
                .Single(control => control.AutoScroll);
            if (scrollPanel.HorizontalScroll.Visible)
            {
                throw new InvalidOperationException(
                    $"Horizontal scrolling appeared at {form.ClientSize.Width}x{form.ClientSize.Height}.");
            }
        }

        form.Close();
        Application.DoEvents();
    }

    private static void ProcessPendingLayout(Control control)
    {
        control.PerformLayout();
        Application.DoEvents();
        control.PerformLayout();
        Application.DoEvents();
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
