#nullable enable

namespace KelpieMCPTray;

internal sealed partial class AboutForm
{
    private Label trayValue = null!;
    private Label serverValue = null!;
    private Label bundleValue = null!;
    private Label companyValue = null!;

    private void InitializeComponent()
    {
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 8,
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label { AutoSize = true, Margin = new Padding(3, 3, 3, 14) };
        title.Font = new Font(title.Font.FontFamily, 16, FontStyle.Bold);
        resources.ApplyResources(title, "title");
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        trayValue = AddRow(layout, resources, "trayLabel", 1);
        serverValue = AddRow(layout, resources, "serverLabel", 2);
        bundleValue = AddRow(layout, resources, "bundleLabel", 3);
        companyValue = AddRow(layout, resources, "companyLabel", 4);
        var license = AddRow(layout, resources, "licenseLabel", 5);
        resources.ApplyResources(license, "license");

        var close = new Button { AutoSize = true, MinimumSize = new Size(88, 32), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
        resources.ApplyResources(close, "close");
        close.Click += (_, _) => Close();
        layout.Controls.Add(close, 1, 7);

        Controls.Add(layout);
        resources.ApplyResources(this, "$this");
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 284);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AcceptButton = close;
        CancelButton = close;
    }

    private static Label AddRow(TableLayoutPanel layout, System.ComponentModel.ComponentResourceManager resources, string name, int row)
    {
        var caption = new Label { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
        resources.ApplyResources(caption, name);
        var value = new Label { AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
        layout.Controls.Add(caption, 0, row);
        layout.Controls.Add(value, 1, row);
        return value;
    }
}
