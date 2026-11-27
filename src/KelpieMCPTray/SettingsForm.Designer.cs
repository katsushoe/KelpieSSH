#nullable enable
namespace KelpieMCPTray;

partial class SettingsForm
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel layout = null!;
    private Label title = null!;
    private Label endpointLabel = null!;
    private Label endpointValue = null!;
    private CheckBox startupLogon = null!;
    private CheckBox startupServer = null!;
    private CheckBox startupUpdate = null!;
    private Label feedLabel = null!;
    private TextBox feed = null!;
    private Label help = null!;
    private Label status = null!;
    private FlowLayoutPanel buttons = null!;
    private Button save = null!;
    private Button cancel = null!;
    private Button logs = null!;
    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
        layout = new TableLayoutPanel(); title = new Label(); endpointLabel = new Label(); endpointValue = new Label();
        startupLogon = new CheckBox(); startupServer = new CheckBox(); startupUpdate = new CheckBox();
        feedLabel = new Label(); feed = new TextBox(); help = new Label(); status = new Label();
        buttons = new FlowLayoutPanel(); save = new Button(); cancel = new Button(); logs = new Button();
        SuspendLayout(); layout.SuspendLayout(); buttons.SuspendLayout();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(24);
        layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowCount = 10;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title.Text = resources.GetString("title.Text");
        title.Font = new Font(title.Font.FontFamily, 16, FontStyle.Bold);
        title.Dock = DockStyle.Fill;
        layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        endpointLabel.Text = resources.GetString("endpointLabel.Text"); endpointLabel.Dock = DockStyle.Fill;
        endpointValue.AutoSize = true; endpointValue.Dock = DockStyle.Fill;
        layout.Controls.Add(endpointLabel, 0, 1); layout.Controls.Add(endpointValue, 1, 1);
        startupLogon.Text = resources.GetString("startupLogon.Text"); startupLogon.Dock = DockStyle.Fill;
        startupServer.Text = resources.GetString("startupServer.Text"); startupServer.Dock = DockStyle.Fill;
        startupUpdate.Text = resources.GetString("startupUpdate.Text"); startupUpdate.Dock = DockStyle.Fill;
        layout.Controls.Add(startupLogon, 0, 2); layout.SetColumnSpan(startupLogon, 2);
        layout.Controls.Add(startupServer, 0, 3); layout.SetColumnSpan(startupServer, 2);
        layout.Controls.Add(startupUpdate, 0, 4); layout.SetColumnSpan(startupUpdate, 2);
        feedLabel.Text = resources.GetString("feedLabel.Text"); feedLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(feedLabel, 0, 5); layout.SetColumnSpan(feedLabel, 2);
        feed.Dock = DockStyle.Fill;
        layout.Controls.Add(feed, 0, 6); layout.SetColumnSpan(feed, 2);
        help.Text = resources.GetString("help.Text"); help.Dock = DockStyle.Fill;
        layout.Controls.Add(help, 0, 7); layout.SetColumnSpan(help, 2);
        status.Text = resources.GetString("status.Text"); status.Dock = DockStyle.Fill; status.ForeColor = Color.DarkSlateGray;
        layout.Controls.Add(status, 0, 8); layout.SetColumnSpan(status, 2);
        buttons.Dock = DockStyle.Bottom; buttons.AutoSize = true; buttons.FlowDirection = FlowDirection.RightToLeft;
        save.Text = resources.GetString("save.Text"); save.AutoSize = true; save.Padding = new Padding(14, 6, 14, 6);
        cancel.Text = resources.GetString("cancel.Text"); cancel.AutoSize = true; cancel.Padding = new Padding(14, 6, 14, 6);
        logs.Text = resources.GetString("logs.Text"); logs.AutoSize = true; logs.Padding = new Padding(14, 6, 14, 6);
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); buttons.Controls.Add(logs);
        layout.Controls.Add(buttons, 0, 9); layout.SetColumnSpan(buttons, 2);
        AutoScaleDimensions = new SizeF(7, 15); AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(760, 520); MinimumSize = new Size(700, 540);
        Text = resources.GetString("$this.Text"); StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        buttons.ResumeLayout(false); buttons.PerformLayout(); layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
    }
}
