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
			this.SelectNspFileToCompressButton = new System.Windows.Forms.Button();
			this.DebugOutput = new System.Windows.Forms.RichTextBox();
			this.SelectNspDialog = new System.Windows.Forms.OpenFileDialog();
			this.TaskQueue = new System.Windows.Forms.ListBox();
			this.SelectNszFileToDecompressButton = new System.Windows.Forms.Button();
			this.RunButton = new System.Windows.Forms.Button();
			this.SelectNszDialog = new System.Windows.Forms.OpenFileDialog();
			this.SuspendLayout();
			// 
			// SelectNspFileToCompressButton
			// 
			this.SelectNspFileToCompressButton.BackColor = System.Drawing.Color.Lime;
			this.SelectNspFileToCompressButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.SelectNspFileToCompressButton.Location = new System.Drawing.Point(42, 36);
			this.SelectNspFileToCompressButton.Name = "SelectNspFileToCompressButton";
			this.SelectNspFileToCompressButton.Size = new System.Drawing.Size(393, 156);
			this.SelectNspFileToCompressButton.TabIndex = 31;
			this.SelectNspFileToCompressButton.Text = "Select NSP files to Compress";
			this.SelectNspFileToCompressButton.UseVisualStyleBackColor = false;
			this.SelectNspFileToCompressButton.Click += new System.EventHandler(this.SelectNspFileToCompressButton_Click);
			// 
			// DebugOutput
			// 
			this.DebugOutput.BackColor = System.Drawing.Color.Black;
			this.DebugOutput.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.DebugOutput.ForeColor = System.Drawing.Color.White;
			this.DebugOutput.Location = new System.Drawing.Point(42, 504);
			this.DebugOutput.Name = "DebugOutput";
			this.DebugOutput.ReadOnly = true;
			this.DebugOutput.Size = new System.Drawing.Size(1220, 545);
			this.DebugOutput.TabIndex = 28;
			this.DebugOutput.Text = "";
			this.DebugOutput.TextChanged += new System.EventHandler(this.DebugOutput_TextChanged);
			// 
			// SelectNspDialog
			// 
			this.SelectNspDialog.Filter = "Switch Package (*.nsp)|*.nsp";
			this.SelectNspDialog.Multiselect = true;
			this.SelectNspDialog.Title = "Select input NSP fIles...";
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
			this.TaskQueue.Size = new System.Drawing.Size(1220, 263);
			this.TaskQueue.TabIndex = 33;
			this.TaskQueue.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.listBox_KeyDown);
			// 
			// SelectNszFileToDecompressButton
			// 
			this.SelectNszFileToDecompressButton.BackColor = System.Drawing.Color.DeepSkyBlue;
			this.SelectNszFileToDecompressButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.SelectNszFileToDecompressButton.Location = new System.Drawing.Point(458, 36);
			this.SelectNszFileToDecompressButton.Name = "SelectNszFileToDecompressButton";
			this.SelectNszFileToDecompressButton.Size = new System.Drawing.Size(393, 156);
			this.SelectNszFileToDecompressButton.TabIndex = 36;
			this.SelectNszFileToDecompressButton.Text = "Select NSZ files to Decompress";
			this.SelectNszFileToDecompressButton.UseVisualStyleBackColor = false;
			this.SelectNszFileToDecompressButton.Click += new System.EventHandler(this.SelectNszFileToDecompressButton_Click);
			// 
			// RunButton
			// 
			this.RunButton.BackColor = System.Drawing.Color.DarkOrange;
			this.RunButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 36F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.RunButton.Location = new System.Drawing.Point(872, 36);
			this.RunButton.Name = "RunButton";
			this.RunButton.Size = new System.Drawing.Size(393, 156);
			this.RunButton.TabIndex = 37;
			this.RunButton.Text = "RUN!";
			this.RunButton.UseVisualStyleBackColor = false;
			this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
			// 
			// SelectNszDialog
			// 
			this.SelectNszDialog.Filter = "Compressed Switch File (*.nsz)|*.nsz";
			this.SelectNszDialog.Multiselect = true;
			this.SelectNszDialog.Title = "Select input nsz fIles...";
			// 
			// Frontend
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.DimGray;
			this.ClientSize = new System.Drawing.Size(1305, 1076);
			this.Controls.Add(this.RunButton);
			this.Controls.Add(this.SelectNszFileToDecompressButton);
			this.Controls.Add(this.TaskQueue);
			this.Controls.Add(this.SelectNspFileToCompressButton);
			this.Controls.Add(this.DebugOutput);
			this.MaximizeBox = false;
			this.Name = "Frontend";
			this.Text = "nsZip File Manager";
			this.Load += new System.EventHandler(this.Form1_Load);
			this.ResumeLayout(false);

        }

        #endregion
		private System.Windows.Forms.Button SelectNspFileToCompressButton;
		private System.Windows.Forms.RichTextBox DebugOutput;
		private System.Windows.Forms.OpenFileDialog SelectNspDialog;
		private System.Windows.Forms.ListBox TaskQueue;
		private System.Windows.Forms.Button SelectNszFileToDecompressButton;
		private System.Windows.Forms.Button RunButton;
		private System.Windows.Forms.OpenFileDialog SelectNszDialog;
	}
}

