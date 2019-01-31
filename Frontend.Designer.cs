namespace nsZip
{
    partial class Frontend
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Frontend));
			this.SelectNspFileToCompressButton = new System.Windows.Forms.Button();
			this.DebugOutput = new System.Windows.Forms.RichTextBox();
			this.SelectNspXciDialog = new System.Windows.Forms.OpenFileDialog();
			this.TaskQueue = new System.Windows.Forms.ListBox();
			this.SelectNszFileToDecompressButton = new System.Windows.Forms.Button();
			this.RunButton = new System.Windows.Forms.Button();
			this.SelectNspzDialog = new System.Windows.Forms.OpenFileDialog();
			this.SelectOutputDictionaryButton = new System.Windows.Forms.Button();
			this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
			this.SelectOutputDictionaryDialog = new System.Windows.Forms.FolderBrowserDialog();
			this.flowLayoutPanel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// SelectNspFileToCompressButton
			// 
			this.SelectNspFileToCompressButton.BackColor = System.Drawing.Color.Lime;
			this.SelectNspFileToCompressButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.SelectNspFileToCompressButton.Location = new System.Drawing.Point(10, 3);
			this.SelectNspFileToCompressButton.Margin = new System.Windows.Forms.Padding(10, 3, 10, 3);
			this.SelectNspFileToCompressButton.Name = "SelectNspFileToCompressButton";
			this.SelectNspFileToCompressButton.Size = new System.Drawing.Size(393, 156);
			this.SelectNspFileToCompressButton.TabIndex = 1;
			this.SelectNspFileToCompressButton.Text = "Select NSP/XCI files to Compress";
			this.SelectNspFileToCompressButton.UseVisualStyleBackColor = false;
			this.SelectNspFileToCompressButton.Click += new System.EventHandler(this.SelectNspFileToCompressButton_Click);
			// 
			// DebugOutput
			// 
			this.DebugOutput.BackColor = System.Drawing.Color.Black;
			this.DebugOutput.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.DebugOutput.ForeColor = System.Drawing.Color.White;
			this.DebugOutput.Location = new System.Drawing.Point(42, 503);
			this.DebugOutput.Name = "DebugOutput";
			this.DebugOutput.ReadOnly = true;
			this.DebugOutput.Size = new System.Drawing.Size(1653, 545);
			this.DebugOutput.TabIndex = 100;
			this.DebugOutput.Text = "";
			this.DebugOutput.TextChanged += new System.EventHandler(this.DebugOutput_TextChanged);
			// 
			// SelectNspXciDialog
			// 
			this.SelectNspXciDialog.Filter = "Switch Games (*.nsp;*.xci)|*.nsp;*.xci|Switch Package (*.nsp)|*.ns|Switch Cartrid" +
    "ge (*.xci)|*.xci";
			this.SelectNspXciDialog.Multiselect = true;
			this.SelectNspXciDialog.Title = "Select input NSP fIles...";
			// 
			// TaskQueue
			// 
			this.TaskQueue.BackColor = System.Drawing.Color.Black;
			this.TaskQueue.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.TaskQueue.ForeColor = System.Drawing.Color.Lime;
			this.TaskQueue.FormattingEnabled = true;
			this.TaskQueue.HorizontalScrollbar = true;
			this.TaskQueue.ItemHeight = 37;
			this.TaskQueue.Location = new System.Drawing.Point(42, 223);
			this.TaskQueue.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.TaskQueue.Name = "TaskQueue";
			this.TaskQueue.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
			this.TaskQueue.Size = new System.Drawing.Size(1653, 263);
			this.TaskQueue.TabIndex = 5;
			this.TaskQueue.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.listBox_KeyDown);
			// 
			// SelectNszFileToDecompressButton
			// 
			this.SelectNszFileToDecompressButton.BackColor = System.Drawing.Color.DeepSkyBlue;
			this.SelectNszFileToDecompressButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.SelectNszFileToDecompressButton.Location = new System.Drawing.Point(423, 3);
			this.SelectNszFileToDecompressButton.Margin = new System.Windows.Forms.Padding(10, 3, 10, 3);
			this.SelectNszFileToDecompressButton.Name = "SelectNszFileToDecompressButton";
			this.SelectNszFileToDecompressButton.Size = new System.Drawing.Size(393, 156);
			this.SelectNszFileToDecompressButton.TabIndex = 2;
			this.SelectNszFileToDecompressButton.Text = "Select NSPZ files to Decompress";
			this.SelectNszFileToDecompressButton.UseVisualStyleBackColor = false;
			this.SelectNszFileToDecompressButton.Click += new System.EventHandler(this.SelectNszFileToDecompressButton_Click);
			// 
			// RunButton
			// 
			this.RunButton.BackColor = System.Drawing.Color.DarkOrange;
			this.RunButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 36F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.RunButton.Location = new System.Drawing.Point(1249, 3);
			this.RunButton.Margin = new System.Windows.Forms.Padding(10, 3, 10, 3);
			this.RunButton.Name = "RunButton";
			this.RunButton.Size = new System.Drawing.Size(393, 156);
			this.RunButton.TabIndex = 4;
			this.RunButton.Text = "RUN!";
			this.RunButton.UseVisualStyleBackColor = false;
			this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
			// 
			// SelectNspzDialog
			// 
			this.SelectNspzDialog.Filter = "Compressed Switch File (*.nspz)|*.nspz";
			this.SelectNspzDialog.Multiselect = true;
			this.SelectNspzDialog.Title = "Select input nspz fIles...";
			// 
			// SelectOutputDictionaryButton
			// 
			this.SelectOutputDictionaryButton.BackColor = System.Drawing.Color.Gold;
			this.SelectOutputDictionaryButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.SelectOutputDictionaryButton.Location = new System.Drawing.Point(836, 3);
			this.SelectOutputDictionaryButton.Margin = new System.Windows.Forms.Padding(10, 3, 10, 3);
			this.SelectOutputDictionaryButton.Name = "SelectOutputDictionaryButton";
			this.SelectOutputDictionaryButton.Size = new System.Drawing.Size(393, 156);
			this.SelectOutputDictionaryButton.TabIndex = 3;
			this.SelectOutputDictionaryButton.Text = "Select Output Dictionary";
			this.SelectOutputDictionaryButton.UseVisualStyleBackColor = false;
			this.SelectOutputDictionaryButton.Click += new System.EventHandler(this.SelectOutputDictionaryButton_Click);
			// 
			// flowLayoutPanel1
			// 
			this.flowLayoutPanel1.Controls.Add(this.SelectNspFileToCompressButton);
			this.flowLayoutPanel1.Controls.Add(this.SelectNszFileToDecompressButton);
			this.flowLayoutPanel1.Controls.Add(this.SelectOutputDictionaryButton);
			this.flowLayoutPanel1.Controls.Add(this.RunButton);
			this.flowLayoutPanel1.Location = new System.Drawing.Point(42, 27);
			this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
			this.flowLayoutPanel1.Name = "flowLayoutPanel1";
			this.flowLayoutPanel1.Size = new System.Drawing.Size(1653, 173);
			this.flowLayoutPanel1.TabIndex = 39;
			// 
			// SelectOutputDictionaryDialog
			// 
			this.SelectOutputDictionaryDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
			// 
			// Frontend
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			this.BackColor = System.Drawing.Color.DimGray;
			this.ClientSize = new System.Drawing.Size(1724, 1076);
			this.Controls.Add(this.flowLayoutPanel1);
			this.Controls.Add(this.TaskQueue);
			this.Controls.Add(this.DebugOutput);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "Frontend";
			this.Text = "nsZip File Manager v1.0.0";
			this.Load += new System.EventHandler(this.Form1_Load);
			this.flowLayoutPanel1.ResumeLayout(false);
			this.ResumeLayout(false);

        }

        #endregion
		private System.Windows.Forms.Button SelectNspFileToCompressButton;
		private System.Windows.Forms.RichTextBox DebugOutput;
		private System.Windows.Forms.OpenFileDialog SelectNspXciDialog;
		private System.Windows.Forms.ListBox TaskQueue;
		private System.Windows.Forms.Button SelectNszFileToDecompressButton;
		private System.Windows.Forms.Button RunButton;
		private System.Windows.Forms.OpenFileDialog SelectNspzDialog;
		private System.Windows.Forms.Button SelectOutputDictionaryButton;
		private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
		private System.Windows.Forms.FolderBrowserDialog SelectOutputDictionaryDialog;
	}
}

