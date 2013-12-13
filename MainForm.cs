using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DataManagementTool {
    public partial class MainForm : Form {
        private const string MessageTitle = "図鑑データ管理ツール";
        Dictionary<int, ClassData> dictionaryData;
        private string dbConnectionString = "";
        private string webServerUrl = "";
        private string ftpServerUrl = "";
        private string ftpUser = "";
        private string ftpPassword = "";
        private int m_ParentID = 0;     // 選択しているカテゴリのID

        public MainForm() {
            InitializeComponent();
        }

        // フォームロード
        private void MainForm_Load(object sender, EventArgs e) {
            // 図鑑DB存在チェック
            if (! File.Exists(Application.StartupPath + @"\0000000000000001.db")) {
                MessageBox.Show("図鑑のDBファイルが見つかりません。アプリケーションを終了します。", 
                    MessageTitle,
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
                this.Close();

                return;
            }

            // 設定用DB存在チェック
            if (! File.Exists(Application.StartupPath + @"\Preference.db")) {
                MessageBox.Show("設定用DBファイルが見つかりません。アプリケーションを終了します。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                this.Close();

                return;
            }

            // ワークDBの作成
            File.Copy(Application.StartupPath + @"\0000000000000001.db", 
                Application.StartupPath + @"\0000000000000001_work.db", true);

            // 各値をセット
            dbConnectionString = "Data Source=" + Application.StartupPath + @"\0000000000000001_work.db";
            using (SQLiteConnection cn = new SQLiteConnection("Data Source=" + Application.StartupPath + @"\Preference.db")) {
                cn.Open();
                using (SQLiteCommand cmd = cn.CreateCommand()) {
                    cmd.CommandText = "SELECT * FROM Preference WHERE PKey = 'K'";
                    using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                        reader.Read();
                        webServerUrl = reader["WebServerUrl"].ToString();
                        ftpServerUrl = reader["FtpServerUrl"].ToString();
                        ftpUser = reader["FtpUserName"].ToString();
                        ftpPassword = reader["FtpPassword"].ToString();
                    }
                }
            }

            // ドラッグ設定
            ClassTreeView.AllowDrop = true;
            TmbPictureBox.AllowDrop = true;
            Detail1PictureBox.AllowDrop = true;
            Detail2PictureBox.AllowDrop = true;
            Detail3PictureBox.AllowDrop = true;
            Detail4PictureBox.AllowDrop = true;
            Detail5PictureBox.AllowDrop = true;

            SetTreeView();

            //// DB接続
            //using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString)) {
            //    cn.Open();
            //    // ツリーの作成
            //    using (SQLiteCommand cmd = cn.CreateCommand()) {
            //        // ツリー初期値のセット
            //        cmd.CommandText = "SELECT * FROM PictureBook WHERE ParentId = 0 ORDER BY SortNumber";
            //        using (SQLiteDataReader reader = cmd.ExecuteReader()) {
            //            // データ保存用ディクショナリ
            //            dictionaryData = new Dictionary<int, ClassData>();

            //            // ルートノードのセット
            //            while (reader.Read()) {
            //                TreeNode treeNode = new TreeNode(reader["Title"].ToString());
            //                treeNode.Tag = int.Parse(reader["Id"].ToString());
            //                this.ClassTreeView.Nodes.Add(treeNode);

            //                // 分類データをディクショナリに保存
            //                ClassData classData = new ClassData();
            //                classData.TmbUrl = reader["Thumbnail"].ToString();
            //                classData.DetailFlag = reader["DetailFlag"].ToString();
            //                classData.ImageUrl = reader["Image"].ToString();
            //                classData.Detail = reader["Detail"].ToString();
            //                dictionaryData.Add(int.Parse(reader["Id"].ToString()), classData);
            //            }
            //        }
            //    }

            //    // ルート分類の子をセット
            //    foreach (TreeNode rootNode in ClassTreeView.Nodes) {
            //        TreeNode treeNode = (TreeNode)rootNode;
            //        ClassData classData = dictionaryData[(int)treeNode.Tag];

            //        classData.IsFoldersAdded = true;
            //        SetChildTreeNodes(cn, treeNode);
            //    }

            //    cn.Close();
            //}
        }

        private void SetTreeView()
        {
            ClassTreeView.Nodes.Clear();

            // DB接続
            using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString))
            {
                cn.Open();
                // ツリーの作成
                using (SQLiteCommand cmd = cn.CreateCommand())
                {
                    // ツリー初期値のセット
                    cmd.CommandText = "SELECT * FROM PictureBook WHERE ParentId = 0 ORDER BY SortNumber";
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        // データ保存用ディクショナリ
                        dictionaryData = new Dictionary<int, ClassData>();

                        // ルートノードのセット
                        while (reader.Read())
                        {
                            TreeNode treeNode = new TreeNode(reader["Title"].ToString());
                            treeNode.Tag = int.Parse(reader["Id"].ToString());
                            this.ClassTreeView.Nodes.Add(treeNode);

                            // 分類データをディクショナリに保存
                            ClassData classData = new ClassData();
                            classData.TmbUrl = reader["Thumbnail"].ToString();
                            classData.DetailFlag = reader["DetailFlag"].ToString();
                            classData.ImageUrl = reader["Image"].ToString();
                            classData.Detail = reader["Detail"].ToString();
                            dictionaryData.Add(int.Parse(reader["Id"].ToString()), classData);
                        }
                    }
                }

                // ルート分類の子をセット
                foreach (TreeNode rootNode in ClassTreeView.Nodes)
                {
                    TreeNode treeNode = (TreeNode)rootNode;
                    ClassData classData = dictionaryData[(int)treeNode.Tag];

                    classData.IsFoldersAdded = true;
                    SetChildTreeNodes(cn, treeNode);
                }

                cn.Close();
            }
        }

        // ツリーオープン前イベント
        private void ClassTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e) {
            Cursor.Current = Cursors.WaitCursor;

            SQLiteConnection cn = new SQLiteConnection(this.dbConnectionString);
            cn.Open();

            // 子ノードの子ノードをセット
            TreeNode currentNode = (TreeNode)e.Node;
            foreach (TreeNode treeNode in currentNode.Nodes) {
                TreeNode childNode = (TreeNode)treeNode;
                ClassData classData = dictionaryData[(int)childNode.Tag];
                if (! classData.IsFoldersAdded) {
                    classData.IsFoldersAdded = true;
                    SetChildTreeNodes(cn, childNode);
                }
            }
            
            cn.Close();
            Cursor.Current = Cursors.Default;
        }

        // ツリービュー選択イベント
        private void ClassTreeView_AfterSelect(object sender, TreeViewEventArgs e) {
            Cursor.Current = Cursors.WaitCursor;

            TreeNode treeNode = e.Node;
            TitleTextBox.Text = treeNode.Text;

            ClassData classData = dictionaryData[(int)treeNode.Tag];

            // Id
            IdLabel.Text = "Id : " + treeNode.Tag.ToString();
            m_ParentID = (int)treeNode.Tag;

            // サムネイル
            if (classData.TmbUrl == "") {
                TmbPictureBox.ImageLocation = "";
            } else { 
                TmbPictureBox.ImageLocation = webServerUrl + classData.TmbUrl;
            }
            TmbFileNameLinkLabel.Text = Path.GetFileName(classData.TmbUrl);
            TmbFileNameLinkLabel.LinkVisited = false;

            // 詳細画像PictureBoxの初期化
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;
            foreach (PictureBox imageUrl in detailImages) {
                imageUrl.ImageLocation = "";
            }

            // 詳細画像ファイル名Labelの初期化
            LinkLabel[] detailFileNames = new LinkLabel[5];
            detailFileNames[0] = DetailFileName1LinkLabel;
            detailFileNames[1] = DetailFileName2LinkLabel;
            detailFileNames[2] = DetailFileName3LinkLabel;
            detailFileNames[3] = DetailFileName4LinkLabel;
            detailFileNames[4] = DetailFileName5LinkLabel;
            foreach (LinkLabel fileName in detailFileNames) {
                fileName.Text = "";
                fileName.LinkVisited = false;
            }

            // 詳細データ
            string[] imageUrls = classData.ImageUrl.Split(',');
            if (classData.DetailFlag == "1") {
                DetailCheckBox.Checked = true;
                DetailTextBox.Text = classData.Detail;
                if (imageUrls[0] != "") { 
                    for (int i = 0; i < imageUrls.Length; i++) {
                        detailImages[i].ImageLocation = webServerUrl + imageUrls[i];
                        detailFileNames[i].Text = Path.GetFileName(imageUrls[i]);
                    }
                }
            } else {
                DetailCheckBox.Checked = false;
                DetailTextBox.Text = "";
            }

            // 詳細コントロール設定
            if (DetailCheckBox.Checked) {
                DetailTextBox.Enabled = true;
            } else {
                DetailTextBox.Enabled = false;
            }

            // 選択ノードの階層を表示
            ClassLevelLabel.Text = "第" + (treeNode.Level + 1) + "階層";

            // 画像保存フォルダ名
            SaveFolderLabel.Text = "FTP保存フォルダ名 : " + GetImageFolder((int)treeNode.Tag);

            Cursor.Current = Cursors.Default;
        }

        // 子ノードのセット、"+"を表示させるには子ノードをあらかじめセットする必要がある
        private void SetChildTreeNodes(SQLiteConnection cn, TreeNode treeNode) {
            using (SQLiteCommand cmd = cn.CreateCommand()) {
                cmd.CommandText = "SELECT * FROM PictureBook WHERE ParentId = " + (int)treeNode.Tag + " ORDER BY SortNumber";
                using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        // ノードを追加
                        TreeNode childNode = new TreeNode(reader["Title"].ToString());
                        childNode.Tag = int.Parse(reader["Id"].ToString());
                        treeNode.Nodes.Add(childNode);

                        // 分類データをディクショナリに保存
                        ClassData classData = new ClassData();
                        classData.TmbUrl = reader["Thumbnail"].ToString();
                        classData.DetailFlag = reader["DetailFlag"].ToString();
                        classData.ImageUrl = reader["Image"].ToString();
                        classData.Detail = reader["Detail"].ToString();
                        dictionaryData.Add(int.Parse(reader["Id"].ToString()), classData);
                    }
                }
            }
        }

        // サムネイルリンクラベルクリック
        private void TmbFileNameLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (TmbPictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            TmbFileNameLinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(TmbPictureBox.ImageLocation);
        }

        // 詳細画像1リンクラベルクリック
        private void DetailFileName1LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (Detail1PictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            DetailFileName1LinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(Detail1PictureBox.ImageLocation);
        }

        // 詳細画像2リンクラベルクリック
        private void DetailFileName2LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (Detail2PictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            DetailFileName2LinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(Detail2PictureBox.ImageLocation);
        }

        // 詳細画像3リンクラベルクリック
        private void DetailFileName3LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (Detail3PictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            DetailFileName3LinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(Detail3PictureBox.ImageLocation);
        }

        // 詳細画像4リンクラベルクリック
        private void DetailFileName4LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (Detail4PictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            DetailFileName4LinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(Detail4PictureBox.ImageLocation);
        }

        // 詳細画像5リンクラベルクリック
        private void DetailFileName5LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            // URLチェック
            if (Detail5PictureBox.ImageLocation == "") {
                return;
            }

            // リンク先に移動したことにする
            DetailFileName5LinkLabel.LinkVisited = true;

            // ブラウザで開く
            System.Diagnostics.Process.Start(Detail5PictureBox.ImageLocation);
        }

        // 画像保存フォルダ名の取得
        private string GetImageFolder(int id) {
            return (id / 100).ToString();
        }

        // ノードがドラッグされた時
        private void ClassTreeView_ItemDrag(object sender, ItemDragEventArgs e) {
            TreeView tv = (TreeView)sender;
            tv.SelectedNode = (TreeNode)e.Item;
            tv.Focus();

            // ノードのドラッグを開始する
            DragDropEffects dde = tv.DoDragDrop(e.Item, DragDropEffects.All);

            // 移動した時は、ドラッグしたノードを削除する
            if ((dde & DragDropEffects.Move) == DragDropEffects.Move) {
                tv.Nodes.Remove((TreeNode)e.Item);
            }
        }

        // ドラッグしている時
        private void ClassTreeView_DragOver(object sender, DragEventArgs e) {
            // ドラッグされているデータがTreeNodeか調べる
            if (e.Data.GetDataPresent(typeof(TreeNode))) {
                if ((e.KeyState & 8) == 8 && (e.AllowedEffect & DragDropEffects.Copy) == DragDropEffects.Copy) {
                    // Ctrlキーが押されていればCopy
                    // "8"はCtrlキーを表す
                    e.Effect = DragDropEffects.Copy;
                } else if ((e.AllowedEffect & DragDropEffects.Move) == DragDropEffects.Move) {
                    // 何も押されていなければMove
                    e.Effect = DragDropEffects.Move;
                } else { 
                    e.Effect = DragDropEffects.None;
                }
            } else {
                // TreeNodeでなければ受け入れない
                e.Effect = DragDropEffects.None;
            }

            // マウス下のNodeを選択する
            if (e.Effect != DragDropEffects.None) {
                TreeView tv = (TreeView)sender;

                // マウスのあるNodeを取得する
                TreeNode target = tv.GetNodeAt(tv.PointToClient(new Point(e.X, e.Y)));

                //ドラッグされているNodeを取得する
                TreeNode source = (TreeNode)e.Data.GetData(typeof(TreeNode));

                // マウス下のNodeがドロップ先として適切か調べる
                if (target != null && target != source && ! IsChildNode(source, target)) {
                    // Nodeを選択する
                    if (target.IsSelected == false) { 
                        tv.SelectedNode = target;
                    }
                } else { 
                    e.Effect = DragDropEffects.None;
                }
            }
        }

        // ドロップされたとき
        private void ClassTreeView_DragDrop(object sender, DragEventArgs e) {
            // ドロップされたデータがTreeNodeか調べる
            if (e.Data.GetDataPresent(typeof(TreeNode))) {
                TreeView tv = (TreeView)sender;

                // ドロップされたデータ(TreeNode)を取得
                TreeNode source = (TreeNode)e.Data.GetData(typeof(TreeNode));

                //ドロップ先のTreeNodeを取得する
                TreeNode target = tv.GetNodeAt(tv.PointToClient(new Point(e.X, e.Y)));

                // マウス下のNodeがドロップ先として適切か調べる
                if (target != null && target != source && ! IsChildNode(source, target)) {
                    // ドロップされたNodeのコピーを作成
                    TreeNode cln = (TreeNode)source.Clone();

                    // Nodeを追加
                    target.Nodes.Add(cln);

                    //ドロップ先のNodeを展開
                    target.Expand();

                    // 追加されたNodeを選択
                    tv.SelectedNode = cln;
                } else { 
                    e.Effect = DragDropEffects.None;
                }
            } else { 
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// あるTreeNodeが別のTreeNodeの子ノードか調べる
        /// </summary>
        /// <param name="parentNode">親ノードか調べるTreeNode</param>
        /// <param name="childNode">子ノードか調べるTreeNode</param>
        /// <returns>子ノードの時はTrue</returns>
        private static bool IsChildNode(TreeNode parentNode, TreeNode childNode) {
            if (childNode.Parent == parentNode) {
                return true;
            } else if (childNode.Parent != null) {
                return IsChildNode(parentNode, childNode.Parent);
            } else { 
                return false;
            }
        }

        // 分類の追加
        private void AddClassButton_Click(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;
            string newTitle = "(タイトルを入力してください)";

            // 親IDの取得
            int parentId = 0;
            if (selectedNode.Parent == null) {
                parentId = 0;
            } else {
                TreeNode parentNode = selectedNode.Parent;
                parentId = (int)parentNode.Tag;
            }

            // レコード追加
            int id = 0;
            using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString)) {
                // DB接続
                cn.Open();

                // トランザクション
                using (SQLiteTransaction trans = cn.BeginTransaction()) {
                    // レコード追加
                    using (SQLiteCommand cmd = cn.CreateCommand()) {
                        cmd.CommandText = "INSERT INTO PictureBook"
                            + " (ClassId, Title, ParentId, Thumbnail, DetailFlag, Image, Detail, SortNumber)"
                            + " VALUES ('', '" + newTitle + "', " + parentId + ", '', '', '', '', 0)";
                        cmd.ExecuteNonQuery();
                    }
                    
                    // 追加レコードのIDを取得
                    using (SQLiteCommand cmd = cn.CreateCommand()) {
                        cmd.CommandText = "SELECT MAX(Id) AS MaxId FROM PictureBook";
                        using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                            reader.Read();
                            id = int.Parse(reader["MaxId"].ToString());
                        }
                    }

                    trans.Commit();
                }

                cn.Close();
            }

            // ノード追加
            TreeNode newNode = new TreeNode(newTitle);
            newNode.Tag = id;
            if (selectedNode.Parent == null) {
                ClassTreeView.Nodes.Insert(selectedNode.Index + 1, newNode);
            } else {
                TreeNode parentNode = selectedNode.Parent;
                parentNode.Nodes.Insert(selectedNode.Index + 1, newNode);
            }

            // ディクショナリ登録
            ClassData classData = new ClassData();
            dictionaryData.Add(id, classData);

            // 追加ノードを選択
            ClassTreeView.SelectedNode = newNode;
            TmbPictureBox.ImageLocation = "";
            Detail1PictureBox.ImageLocation = "";
            Detail2PictureBox.ImageLocation = "";
            Detail3PictureBox.ImageLocation = "";
            Detail4PictureBox.ImageLocation = "";
            Detail5PictureBox.ImageLocation = "";
        }

        private void FinishAppButton_Click(object sender, EventArgs e) {
            if (MessageBox.Show("アプリケーションを終了しますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            this.Close();
        }

        // タイトルチェンジイベント
        private void TitleTextBox_TextChanged(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;
            ClassData classData = dictionaryData[(int)selectedNode.Tag];

            // タイトル変更
            selectedNode.Text = TitleTextBox.Text;

            // 変更フラグON
            classData.IsChanged = true;
        }

        // 詳細チェンジイベント
        private void DetailTextBox_TextChanged(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;
            ClassData classData = dictionaryData[(int)selectedNode.Tag];

            // 詳細変更
            classData.Detail = DetailTextBox.Text;

            // 変更フラグON
            classData.IsChanged = true;
        }

        // 詳細フラグチェンジイベント
        private void DetailCheckBox_CheckedChanged(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;
            ClassData classData = dictionaryData[(int)selectedNode.Tag];

            // 詳細フラグの変更
            classData.DetailFlag = DetailCheckBox.Checked == true ? "1" : "";

            // 変更フラグON
            classData.IsChanged = true;

            // 詳細コントロール設定
            if (DetailCheckBox.Checked) {
                DetailTextBox.Enabled = true;
            } else {
                DetailTextBox.Enabled = false;
            }
        }

        // 分類サムネイル画像ドラッグ
        private void ClassThumbnail_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 分類サムネイル画像ドロップ
        private void ClassThumbnail_DragDrop(object sender, DragEventArgs e) {
            // アップロード確認
            if (MessageBox.Show("画像をアップロードしますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // ドロップファイル数チェック
            if (files.Length > 1) {
                MessageBox.Show("複数ファイルのアップロードはできません。",
                MessageTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);

                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // ローカルファイル名
            string localFile = files[0];

            // アップロードファイル名
            string uploadFile = ftpServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/" + (int)selectedNode.Tag + "_tmb.jpg";

            // FTPアップロード
            ftpUploadFile(localFile, uploadFile);

            // 分類データに反映
            ClassData classData = dictionaryData[(int)selectedNode.Tag];
            classData.IsChanged = true;
            classData.TmbUrl = "images/" + GetImageFolder((int)selectedNode.Tag) + "/" + (int)selectedNode.Tag + "_tmb.jpg";

            // 画像を表示
            TmbPictureBox.ImageLocation = webServerUrl + classData.TmbUrl;
        }

        // 詳細画像1ドラッグ
        private void Detail1PictureBox_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 詳細画像1ドロップ
        private void Detail1PictureBox_DragDrop(object sender, DragEventArgs e) {
            DetailPictureDragDrop(e, 0);
        }

        // 詳細画像2ドラッグ
        private void Detail2PictureBox_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 詳細画像2ドロップ
        private void Detail2PictureBox_DragDrop(object sender, DragEventArgs e) {
            DetailPictureDragDrop(e, 1);
        }

        // 詳細画像3ドラッグ
        private void Detail3PictureBox_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 詳細画像3ドロップ
        private void Detail3PictureBox_DragDrop(object sender, DragEventArgs e) {
            DetailPictureDragDrop(e, 2);
        }

        // 詳細画像4ドラッグ
        private void Detail4PictureBox_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 詳細画像4ドロップ
        private void Detail4PictureBox_DragDrop(object sender, DragEventArgs e) {
            DetailPictureDragDrop(e, 3);
        }

        // 詳細画像5ドラッグ
        private void Detail5PictureBox_DragEnter(object sender, DragEventArgs e) {
            if (! e.Data.GetDataPresent(DataFormats.FileDrop)) {
                return;
            }

            e.Effect = DragDropEffects.Copy;
        }

        // 詳細画像5ドロップ
        private void Detail5PictureBox_DragDrop(object sender, DragEventArgs e) {
            DetailPictureDragDrop(e, 4);
        }

        // 詳細画像のドロップ
        private void DetailPictureDragDrop(DragEventArgs e, int index) {
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;

            // アップロード確認
            if (MessageBox.Show("画像をアップロードしますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            // 詳細データかチェック
            if (! DetailCheckBox.Checked) {
                MessageBox.Show("このデータは詳細データではありません。",
                MessageTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);

                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // ドロップファイル数チェック
            if (files.Length > 1) {
                MessageBox.Show("複数ファイルのアップロードはできません。",
                MessageTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);

                return;
            }

            // 飛ばしてセットしてるか
            if (index > 0) {
                if (detailImages[index - 1].ImageLocation == "") {
                    MessageBox.Show("詳細画像" + index + "にドラッグしてください。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

                    return;
                }
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // ローカルファイル名
            string localFile = files[0];

            // アップロードファイル名
            string uploadFile = ftpServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/" 
                + (int)selectedNode.Tag + "_detail_" + (index + 1) + ".jpg";

            // FTPアップロード
            ftpUploadFile(localFile, uploadFile);

            // 画像を表示
            detailImages[index].ImageLocation = webServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/"
                + (int)selectedNode.Tag + "_detail_" + (index + 1) + ".jpg";

            // 分類データに反映
            ClassData classData = dictionaryData[(int)selectedNode.Tag];
            classData.IsChanged = true;
            classData.ImageUrl = GetImageUriData();
        }

        // カンマ区切りのImageの取得
        private string GetImageUriData() {
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;

            string imageData = "";
            foreach (PictureBox detailImage in detailImages) {
                if (detailImage.ImageLocation != "") {
                    imageData += detailImage.ImageLocation.Replace(webServerUrl, "") + ",";
                }
            }

            if (imageData == "") {
                return "";
            } 

            return imageData.Substring(0, imageData.Length - 1);
        }


        // 詳細画像1クリック
        private void Detail1PictureBox_Click(object sender, EventArgs e) {
            if (Detail1PictureBox.ImageLocation == "") {
                return;
            }

            ShowImageWindow(Detail1PictureBox.ImageLocation);
        }

        // 詳細画像2クリック
        private void Detail2PictureBox_Click(object sender, EventArgs e) {
            if (Detail2PictureBox.ImageLocation == "") {
                return;
            }

            ShowImageWindow(Detail2PictureBox.ImageLocation);
        }

        // 詳細画像3クリック
        private void Detail3PictureBox_Click(object sender, EventArgs e) {
            if (Detail3PictureBox.ImageLocation == "") {
                return;
            }

            ShowImageWindow(Detail3PictureBox.ImageLocation);
        }

        // 詳細画像4クリック
        private void Detail4PictureBox_Click(object sender, EventArgs e) {
            if (Detail4PictureBox.ImageLocation == "") {
                return;
            }

            ShowImageWindow(Detail4PictureBox.ImageLocation);
        }

        // 詳細画像5クリック
        private void Detail5PictureBox_Click(object sender, EventArgs e) {
            if (Detail5PictureBox.ImageLocation == "") {
                return;
            }

            ShowImageWindow(Detail5PictureBox.ImageLocation);
        }

        // 詳細画像の表示
        private void ShowImageWindow(string imageLocation) {
            ImageWindowForm detailImageForm = new ImageWindowForm();
            detailImageForm.ImageUrl = imageLocation;
            detailImageForm.ShowDialog(this);
        }

        // WebサーバーURL設定
        private void WebServerURLToolStripMenuItem_Click(object sender, EventArgs e) {
            // 設定画面
            WebServerUrlForm form = new WebServerUrlForm();
            form.ShowDialog(this);
        }

        // Ftpサーバーアカウント
        private void FtpAccountToolStripMenuItem_Click(object sender, EventArgs e) {
            // 設定画面
            FtpAccountForm form = new FtpAccountForm();
            form.ShowDialog(this);
        }

        // ノードのアップ
        private void MoveUpNodeButton_Click(object sender, EventArgs e) {
            // ノードが選択されてない場合
            if (ClassTreeView.SelectedNode == null) {
                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 選択ノードが一番上の場合
            if (selectedNode.Index <= 0) {
                return;
            }

            // 上へ移動（クローン作製インサート&現在ノード削除
            if (selectedNode.Parent == null) {
                // ルートの場合
                TreeNode cloneNode = (TreeNode)selectedNode.Clone();
                int selectedIndex = selectedNode.Index;
                ClassTreeView.Nodes.Remove(selectedNode);
                ClassTreeView.Nodes.Insert(selectedIndex - 1, cloneNode);
                ClassTreeView.SelectedNode = cloneNode;
            } else { 
                // 子ノードの場合
                TreeNode parentNode = selectedNode.Parent;
                TreeNode cloneNode = (TreeNode)selectedNode.Clone();
                int selectedIndex = selectedNode.Index;
                parentNode.Nodes.Remove(selectedNode);
                parentNode.Nodes.Insert(selectedIndex - 1, cloneNode);
                ClassTreeView.SelectedNode = cloneNode;
            }
        }

        // ノードのダウン
        private void MoveDownNodeButton_Click(object sender, EventArgs e) {
            // ノードが選択されてない場合
            if (ClassTreeView.SelectedNode == null) {
                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 同階層のノード層の取得
            int nodeCount = 0;
            if (selectedNode.Parent == null) {
                nodeCount = ClassTreeView.Nodes.Count;
            } else {
                nodeCount = selectedNode.Parent.Nodes.Count;
            }

            // 選択ノードが一番下の場合
            if (selectedNode.Index + 1 >= nodeCount) {
                return;
            }

            // 下へ移動（クローン作製インサート&現在ノード削除）
            if (selectedNode.Parent == null) {
                // ルートの場合
                TreeNode cloneNode = (TreeNode)selectedNode.Clone();
                int selectedIndex = selectedNode.Index;
                ClassTreeView.Nodes.Remove(selectedNode);
                ClassTreeView.Nodes.Insert(selectedIndex + 1, cloneNode);
                ClassTreeView.SelectedNode = cloneNode;
            } else {
                // 子ノードの場合
                TreeNode parentNode = selectedNode.Parent;
                TreeNode cloneNode = (TreeNode)selectedNode.Clone();
                int selectedIndex = selectedNode.Index;
                parentNode.Nodes.Remove(selectedNode);
                parentNode.Nodes.Insert(selectedIndex + 1, cloneNode);
                ClassTreeView.SelectedNode = cloneNode;
            }
        }

        // ルートに移動
        private void MoveToRootButton_Click(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 選択ノードがルートの場合は終了
            if (selectedNode.Parent == null) {
                return;
            }

            // ルートに移動
            TreeNode cloneNode = (TreeNode)selectedNode.Clone();
            int selectedIndex = selectedNode.Index;
            ClassTreeView.Nodes.Remove(selectedNode);
            ClassTreeView.Nodes.Add(cloneNode);
            ClassTreeView.SelectedNode = cloneNode;
        }

        // 分類の削除
        private void DeleteClassButton_Click(object sender, EventArgs e) {
            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 子ノードがある場合は終了
            if (selectedNode.Nodes.Count > 0) {
                MessageBox.Show("親ノードは削除できません。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

                return;
            }

            // 削除確認
            if (MessageBox.Show("データは削除すると元にもどせません。\nデータを削除しますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {

                return;
            }

            int id = (int)selectedNode.Tag;
            int index = selectedNode.Index;

            // レコードの削除
            using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString)) {
                cn.Open();
                // トランザクション
                using (SQLiteTransaction trans = cn.BeginTransaction()) {
                    // レコード追加
                    using (SQLiteCommand cmd = cn.CreateCommand()) {
                        cmd.CommandText = "DELETE FROM PictureBook WHERE Id = " + id;
                        cmd.ExecuteNonQuery();
                    }
                    trans.Commit();
                }
                cn.Close();
            }

            // ディクショナリから削除
            dictionaryData.Remove(id);

            // 画像データの削除
            string ftpPath = ftpServerUrl + "images/" + GetImageFolder(id) + "/";
            if (TmbPictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(TmbPictureBox.ImageLocation), false);
            }
            if (Detail1PictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(Detail1PictureBox.ImageLocation), false);
            }
            if (Detail2PictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(Detail2PictureBox.ImageLocation), false);
            }
            if (Detail3PictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(Detail3PictureBox.ImageLocation), false);
            }
            if (Detail4PictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(Detail4PictureBox.ImageLocation), false);
            }
            if (Detail5PictureBox.ImageLocation != "") {
                ftpDeleteFile(ftpPath + Path.GetFileName(Detail5PictureBox.ImageLocation), false);
            }

            // ノードの削除
            selectedNode.Remove();

            MessageBox.Show("データを削除しました。",
                MessageTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // FTPファイルのアップロード
        private void ftpUploadFile(string localFile, string ftpFile, bool showMsg = true)
        {
            try
            {

                // アップロード先のURI
                Uri uri = new Uri(ftpFile);

                // FtpWebRequestの作成
                System.Net.FtpWebRequest ftpReq = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(uri);

                // ログインユーザー名とパスワードを設定
                ftpReq.Credentials = new System.Net.NetworkCredential(ftpUser, ftpPassword);

                // 接続の高速化
                ftpReq.Proxy = null;

                // MethodにWebRequestMethods.Ftp.UploadFile("STOR")を設定
                ftpReq.Method = System.Net.WebRequestMethods.Ftp.UploadFile;

                // 要求の完了後に接続を閉じる
                ftpReq.KeepAlive = false;

                // バイナリモードで転送する
                ftpReq.UseBinary = true;

                // PASVモードを無効にする
                ftpReq.UsePassive = false;

                //ファイルをアップロードするためのStreamを取得
                System.IO.Stream reqStrm = ftpReq.GetRequestStream();

                //アップロードするファイルを開く
                System.IO.FileStream fs = new System.IO.FileStream(localFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);

                //アップロードStreamに書き込む
                byte[] buffer = new byte[1024];
                while (true)
                {
                    int readSize = fs.Read(buffer, 0, buffer.Length);
                    if (readSize == 0)
                    {
                        break;
                    }
                    reqStrm.Write(buffer, 0, readSize);
                }
                fs.Close();
                reqStrm.Close();

                // FtpWebResponseを取得
                System.Net.FtpWebResponse ftpRes = (System.Net.FtpWebResponse)ftpReq.GetResponse();

                Cursor.Current = Cursors.Default;

                if (showMsg == true)
                {
                    // FTPサーバーから送信されたステータスを表示
                    MessageBox.Show("StatusCode:" + ftpRes.StatusCode + "\n" + "StatusDescription:" + ftpRes.StatusDescription
                        + "\nFTP接続が完了しました。上記のFTPステータスを確認してください。",
                        MessageTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    // エラーの場合のみ出力
                    if (ftpRes.StatusCode != System.Net.FtpStatusCode.ClosingData)
                    {
                        // FTPサーバーから送信されたステータスを表示
                        MessageBox.Show("StatusCode:" + ftpRes.StatusCode + "\n" + "StatusDescription:" + ftpRes.StatusDescription
                            + "\nFTP送信が異常終了しました。上記のFTPステータスを確認してください。",
                            MessageTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }

                // 閉じる
                ftpRes.Close();
            }
            catch (System.Exception ex)
            {
                //すべての例外をキャッチする
                //例外の説明を表示する
                System.Console.WriteLine(ex.Message);
                MessageBox.Show(ex.Message, MessageTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // FTPファイルの削除
        private void ftpDeleteFile(string ftpFile, bool showMsg) {
            // 削除するファイルのURI
            Uri uri = new Uri(ftpFile);

            // FtpWebRequestの作成
            System.Net.FtpWebRequest ftpReq = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(uri);

            // ログインユーザー名とパスワードを設定
            ftpReq.Credentials = new System.Net.NetworkCredential(ftpUser, ftpPassword);

            // 接続の高速化
            ftpReq.Proxy = null;

            // MethodにWebRequestMethods.Ftp.DeleteFile(DELE)を設定
            ftpReq.Method = System.Net.WebRequestMethods.Ftp.DeleteFile;

            // FtpWebResponseを取得
            System.Net.FtpWebResponse ftpRes = (System.Net.FtpWebResponse)ftpReq.GetResponse();

            // FTPサーバーから送信されたステータスを表示
            Console.WriteLine("{0}: {1}", ftpRes.StatusCode, ftpRes.StatusDescription);

            // FTPサーバーから送信されたステータスを表示
            if (showMsg) { 
                MessageBox.Show("StatusCode:" + ftpRes.StatusCode + "\n" + "StatusDescription:" + ftpRes.StatusDescription
                    + "\nFTP接続が完了しました。上記のFTPステータスを確認してください。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            // 閉じる
            ftpRes.Close();
        }

        // FTPファイル名の変更
        private void ftpChangeFileName(string ftpFileFrom, string ftpFileTo) {
            // サイズを取得するファイルのURI
            Uri uri = new Uri(ftpFileFrom);

            // FtpWebRequestの作成
            System.Net.FtpWebRequest ftpReq = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(uri);

            // ログインユーザー名とパスワードを設定
            ftpReq.Credentials = new System.Net.NetworkCredential(ftpUser, ftpPassword);

            // 接続の高速化
            ftpReq.Proxy = null;

            // MethodにWebRequestMethods.Ftp.Rename(RENAME)を設定
            ftpReq.Method = System.Net.WebRequestMethods.Ftp.Rename;

            //変更後の新しいファイル名を設定
            ftpReq.RenameTo = ftpFileTo;

            // FtpWebResponseを取得
            System.Net.FtpWebResponse ftpRes = (System.Net.FtpWebResponse)ftpReq.GetResponse();

            // FTPサーバーから送信されたステータスを表示
            //MessageBox.Show("StatusCode:" + ftpRes.StatusCode + "\n" + "StatusDescription:" + ftpRes.StatusDescription
            //    + "\nFTP接続が完了しました。上記のFTPステータスを確認してください。",
            //    MessageTitle,
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Information);

            //閉じる
            ftpRes.Close();
        }

        // DBの更新
        private void UpdateDbButton_Click(object sender, EventArgs e) {
            // 更新確認
            if (MessageBox.Show("DBを更新しますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            Cursor.Current = Cursors.WaitCursor;

            using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString)) {
                // DB接続
                cn.Open();

                // アップデート文のセット(ParentId、SortNumber)
                SQLiteCommand sortNumberCmd = cn.CreateCommand();
                sortNumberCmd.CommandText = "UPDATE PictureBook SET ParentId = @ParentId, "
                                                + " SortNumber = @SortNumber WHERE Id = @Id";
                sortNumberCmd.Parameters.Add("Id", System.Data.DbType.Int64);
                sortNumberCmd.Parameters.Add("ParentId", System.Data.DbType.Int64);
                sortNumberCmd.Parameters.Add("SortNumber", System.Data.DbType.Int64);

                // アップデート文のセット(データ)
                SQLiteCommand dataCmd = cn.CreateCommand();
                dataCmd.CommandText = "UPDATE PictureBook SET Title = @Title, Thumbnail = @Thumbnail,"
                                        + " DetailFlag = @DetailFlag, Image = @Image, Detail = @Detail WHERE Id = @Id";
                dataCmd.Parameters.Add("Id", System.Data.DbType.Int64);
                dataCmd.Parameters.Add("Title", System.Data.DbType.String);
                dataCmd.Parameters.Add("Thumbnail", System.Data.DbType.String);
                dataCmd.Parameters.Add("DetailFlag", System.Data.DbType.String);
                dataCmd.Parameters.Add("Image", System.Data.DbType.String);
                dataCmd.Parameters.Add("Detail", System.Data.DbType.String);

                // トランザクション
                using (SQLiteTransaction trans = cn.BeginTransaction()) {
                    // 分類データの更新
                    foreach (TreeNode tn in ClassTreeView.Nodes) {
                        UpdateClassData(cn, tn, sortNumberCmd, dataCmd);
                    }
                    // コミット
                    trans.Commit();
                }

                cn.Close();
            }

            // ワークDBを正に
            File.Copy(Application.StartupPath + @"\0000000000000001_work.db",
                            Application.StartupPath + @"\0000000000000001.db", true);

            Cursor.Current = Cursors.Default;

            MessageBox.Show("DBを更新しました。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
        }

        // ソートナンバーのセット
        private void UpdateClassData(SQLiteConnection cn, TreeNode treeNode, SQLiteCommand sortCmd, SQLiteCommand dataCmd) {
            // 分類データ
            ClassData classData = dictionaryData[(int)treeNode.Tag];

            // ParentId、SortNumberの更新
            int parentId = 0;
            if (treeNode.Parent != null) {
                TreeNode parentNode = treeNode.Parent;
                parentId = (int)parentNode.Tag;
            }
            sortCmd.Parameters["Id"].Value = (int)treeNode.Tag;
            sortCmd.Parameters["ParentId"].Value = parentId;
            sortCmd.Parameters["SortNumber"].Value = treeNode.Index;
            sortCmd.ExecuteNonQuery();

            // タイトルor詳細の更新
            if (classData.IsChanged) {
                dataCmd.Parameters["Id"].Value = (int)treeNode.Tag;
                dataCmd.Parameters["Title"].Value = treeNode.Text;
                dataCmd.Parameters["Thumbnail"].Value = classData.TmbUrl;
                dataCmd.Parameters["DetailFlag"].Value = classData.DetailFlag;
                dataCmd.Parameters["Image"].Value = classData.ImageUrl;
                dataCmd.Parameters["Detail"].Value = classData.Detail;
                dataCmd.ExecuteNonQuery();
            }

            // 子ノードへ（再帰）
            foreach (TreeNode tn in treeNode.Nodes) {
                UpdateClassData(cn, tn, sortCmd, dataCmd);
            }
        }

        // 分類IDの作成
        private void UpdateClassIdButton_Click(object sender, EventArgs e) {
            // 更新確認
            if (MessageBox.Show("分類IDを作成しますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            Cursor.Current = Cursors.WaitCursor;

            using (SQLiteConnection cn = new SQLiteConnection(dbConnectionString)) {
                // DB接続
                cn.Open();

                // 分類ID
                SQLiteCommand classIdCmd = cn.CreateCommand();
                classIdCmd.CommandText = "UPDATE PictureBook SET ClassId = @ClassId WHERE Id = @Id";
                classIdCmd.Parameters.Add("Id", System.Data.DbType.Int64);
                classIdCmd.Parameters.Add("ClassId", System.Data.DbType.String);

                // トランザクション
                using (SQLiteTransaction trans = cn.BeginTransaction()) {
                    // 分類IDのセット
                    SQLiteCommand readerCmd = cn.CreateCommand();
                    readerCmd.CommandText = "SELECT * FROM PictureBook WHERE ParentId = 0 ORDER BY SortNumber";
                    using (SQLiteDataReader reader = readerCmd.ExecuteReader()) {
                        int id = 0;
                        int index = 0;
                        string detailFlag = "";
                        while (reader.Read()) {
                            id = int.Parse(reader["Id"].ToString());
                            detailFlag = reader["DetailFlag"].ToString();
                            UpdateClassId(cn, classIdCmd, id, "", index, detailFlag);
                            index++;
                        }
                    }
                    // コミット
                    trans.Commit();
                }

                cn.Close();
            }

            // ワークDBを正に
            File.Copy(Application.StartupPath + @"\0000000000000001_work.db",
                            Application.StartupPath + @"\0000000000000001.db", true);

            Cursor.Current = Cursors.Default;

            MessageBox.Show("分類IDを作成しました。",
                    MessageTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
        }

        // 分類コードのセット
        private void UpdateClassId(SQLiteConnection cn, SQLiteCommand classIdCmd, 
                int id, string classId, int index, string detailFlag) {
            // 分類Idの更新
            string childClassId = classId + String.Format("{0:D3}", index + 1);
            classIdCmd.Parameters["Id"].Value = id;
            classIdCmd.Parameters["ClassId"].Value = childClassId;
            classIdCmd.ExecuteNonQuery();

            if (detailFlag == "1") {
                return;
            }

            // 子ノードへ（再帰）
            SQLiteCommand readerCmd = cn.CreateCommand();
            readerCmd.CommandText = "SELECT * FROM PictureBook WHERE ParentId = " + id + " ORDER BY SortNumber";
            using (SQLiteDataReader reader = readerCmd.ExecuteReader()) {
                int childId = 0;
                int childIndex = 0;
                string childDetailFlag = "";
                while (reader.Read()) {
                    childId = int.Parse(reader["Id"].ToString());
                    childDetailFlag = reader["DetailFlag"].ToString();
                    UpdateClassId(cn, classIdCmd, childId, childClassId, childIndex, childDetailFlag);
                    childIndex++;
                }
            }
        }

        // 画像一括ダウンロード
        private void ImageDownLoadButton_Click(object sender, EventArgs e) {
            System.Net.WebClient wc = new System.Net.WebClient();
            string savePath = Application.StartupPath + @"\Images";
            string fileName = "";

            // フォルダ存在チェック
            if (! Directory.Exists(savePath)) {
                Directory.CreateDirectory(savePath);
            }

            // サムネイル
            if (TmbPictureBox.ImageLocation != "") { 
                fileName = Path.GetFileName(TmbPictureBox.ImageLocation);
                wc.DownloadFile(TmbPictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            // 詳細1
            if (Detail1PictureBox.ImageLocation != "") {
                fileName = Path.GetFileName(Detail1PictureBox.ImageLocation);
                wc.DownloadFile(Detail1PictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            // 詳細2
            if (Detail2PictureBox.ImageLocation != "") {
                fileName = Path.GetFileName(Detail2PictureBox.ImageLocation);
                wc.DownloadFile(Detail2PictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            // 詳細3
            if (Detail3PictureBox.ImageLocation != "") {
                fileName = Path.GetFileName(Detail3PictureBox.ImageLocation);
                wc.DownloadFile(Detail3PictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            // 詳細4
            if (Detail4PictureBox.ImageLocation != "") {
                fileName = Path.GetFileName(Detail4PictureBox.ImageLocation);
                wc.DownloadFile(Detail4PictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            // 詳細5
            if (Detail5PictureBox.ImageLocation != "") {
                fileName = Path.GetFileName(Detail5PictureBox.ImageLocation);
                wc.DownloadFile(Detail5PictureBox.ImageLocation, savePath + "\\" + fileName);
                wc.Dispose();
            }

            MessageBox.Show("画像を保存しました。",
                MessageTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // 詳細画像1削除ボタン
        private void DeleteImage1Button_Click(object sender, EventArgs e) {
            DeleteImage(0);
        }

        // 詳細画像2削除ボタン
        private void DeleteImage2Button_Click(object sender, EventArgs e) {
            DeleteImage(1);
        }

        // 詳細画像3削除ボタン
        private void DeleteImage3Button_Click(object sender, EventArgs e) {
            DeleteImage(2);
        }

        // 詳細画像4削除ボタン
        private void DeleteImage4Button_Click(object sender, EventArgs e) {
            DeleteImage(3);
        }

        // 詳細画像5削除ボタン
        private void DeleteImage5Button_Click(object sender, EventArgs e) {
            DeleteImage(4);
        }

        // 画像の削除
        private void DeleteImage(int index) {
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;

            // 画像なければ終了
            if (detailImages[index].ImageLocation == "") {
                return;
            }

            // 削除確認
            if (MessageBox.Show("画像を削除しますか？",
                    MessageTitle,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.No) {
                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;
            string ftpPath = ftpServerUrl + "images/" + GetImageFolder((int)selectedNode.Tag) + "/";
            string webPath = webServerUrl + "images/" + GetImageFolder((int)selectedNode.Tag) + "/";

            // FTPで画像削除
            ftpDeleteFile(ftpPath + Path.GetFileName(detailImages[index].ImageLocation), true);
            detailImages[index].ImageLocation = "";

            // 画像をずらす
            if (index < 4) { 
                for (int i = index; i < detailImages.Length - 1; i++) {
                    if (detailImages[i + 1].ImageLocation != "") {
                        // ファイル名の変更
                        string sFile = (int)selectedNode.Tag + "_detail_" + (i + 2) + ".jpg";
                        string dFile = (int)selectedNode.Tag + "_detail_" + (i + 1) + ".jpg";
                        ftpChangeFileName(ftpPath + sFile, dFile);

                        // ImageLocationのリフレッシュ
                        detailImages[i].ImageLocation =
                            webPath + (int)selectedNode.Tag + "_detail_" + (i + 1) + ".jpg";
                        detailImages[i + 1].ImageLocation = "";
                    }
                }
            }

            // 分類データの更新
            ClassData classData = dictionaryData[(int)selectedNode.Tag];
            classData.IsChanged = true;
            classData.ImageUrl = GetImageUriData();
        }

        // 詳細画像1右移動
        private void MoveToRight1Button_Click(object sender, EventArgs e) {
            MoveToRight(0);
        }

        // 詳細画像2右移動
        private void MoveToRight2Button_Click(object sender, EventArgs e) {
            MoveToRight(1);
        }

        // 詳細画像3右移動
        private void MoveToRight3Button_Click(object sender, EventArgs e) {
            MoveToRight(2);
        }

        // 詳細画像4右移動
        private void MoveToRight4Button_Click(object sender, EventArgs e) {
            MoveToRight(3);
        }

        // 詳細画像右移動
        private void MoveToRight(int index) {
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;

            // 画像がなければ終了
            if (detailImages[index].ImageLocation == "") {
                return;
            }

            // 右側に画像がなければ終了
            if (detailImages[index + 1].ImageLocation == "") {
                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 画像のリネーム（コピー）
            string sFile = ftpServerUrl + "images/" 
                + GetImageFolder((int)selectedNode.Tag) + "/" + Path.GetFileName(detailImages[index].ImageLocation);
            string dFile = ftpServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/" + Path.GetFileName(detailImages[index + 1].ImageLocation);
            string tmpFile = ftpServerUrl + "images/" + GetImageFolder((int)selectedNode.Tag) + "/tmp.jpg";
            ftpChangeFileName(dFile,  Path.GetFileName(tmpFile));
            ftpChangeFileName(sFile,  Path.GetFileName(dFile));
            ftpChangeFileName(tmpFile, Path.GetFileName(sFile));

            // 画像のリフレッシュ
            detailImages[index].ImageLocation = detailImages[index].ImageLocation;
            detailImages[index + 1].ImageLocation = detailImages[index + 1].ImageLocation;
        }

        // 詳細画像2左移動
        private void MoveToLeft2Button_Click(object sender, EventArgs e) {
            MoveToLeft(1);
        }

        // 詳細画像3左移動
        private void MoveToLeft3Button_Click(object sender, EventArgs e) {
            MoveToLeft(2);
        }

        // 詳細画像4左移動
        private void MoveToLeft4Button_Click(object sender, EventArgs e) {
            MoveToLeft(3);
        }

        // 詳細画像5左移動
        private void MoveToLeft5Button_Click(object sender, EventArgs e) {
            MoveToLeft(4);
        }

        // 詳細画像左移動
        private void MoveToLeft(int index) {
            PictureBox[] detailImages = new PictureBox[5];
            detailImages[0] = Detail1PictureBox;
            detailImages[1] = Detail2PictureBox;
            detailImages[2] = Detail3PictureBox;
            detailImages[3] = Detail4PictureBox;
            detailImages[4] = Detail5PictureBox;

            // 画像がなければ終了
            if (detailImages[index].ImageLocation == "") {
                return;
            }

            // 左側に画像がなければ終了
            if (detailImages[index - 1].ImageLocation == "") {
                return;
            }

            TreeNode selectedNode = ClassTreeView.SelectedNode;

            // 画像のリネーム（コピー）
            string sFile = ftpServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/" + Path.GetFileName(detailImages[index].ImageLocation);
            string dFile = ftpServerUrl + "images/"
                + GetImageFolder((int)selectedNode.Tag) + "/" + Path.GetFileName(detailImages[index - 1].ImageLocation);
            string tmpFile = ftpServerUrl + "images/" + GetImageFolder((int)selectedNode.Tag) + "/tmp.jpg";
            ftpChangeFileName(dFile, Path.GetFileName(tmpFile));
            ftpChangeFileName(sFile, Path.GetFileName(dFile));
            ftpChangeFileName(tmpFile, Path.GetFileName(sFile));

            // 画像のリフレッシュ
            detailImages[index].ImageLocation = detailImages[index].ImageLocation;
            detailImages[index - 1].ImageLocation = detailImages[index - 1].ImageLocation;
        }

        // タイトル<BR>
        private void TitleBrButton_Click(object sender, EventArgs e) {
            TitleTextBox.Text = TitleTextBox.Text.Substring(0, TitleTextBox.SelectionStart)
                              + "<br>"
                              + TitleTextBox.Text.Substring(TitleTextBox.SelectionStart
                              + TitleTextBox.SelectionLength);
        }

        // 詳細<BR>
        private void DetailBrButton_Click(object sender, EventArgs e) {
            if (! DetailTextBox.Enabled) {
                return;
            }

            DetailTextBox.Text = DetailTextBox.Text.Substring(0, DetailTextBox.SelectionStart)
                              + "<br>"
                              + DetailTextBox.Text.Substring(DetailTextBox.SelectionStart
                              + DetailTextBox.SelectionLength);
        }

        // 2013/11/9 I.Takagi(CI) Add
        // データ一括取り込み
        private void GetDataButton_Click(object sender, EventArgs e)
        {

            // 変数定義
            int iSortNumber = 0;
            int iId = 0;
            SQLiteConnection cn = new SQLiteConnection(dbConnectionString);
            SQLiteCommand cmd;
            string folderPath;
            string localFile;
            string uploadFile;      // アップロードファイル名
            int iDetailIndex;       // 詳細画像インデックス
            string insertImageString;

            this.Enabled = false;

            try
            {
                if (MessageBox.Show("他のデータを更新した場合は、先に【DBの更新】を実行して下さい。\nデータを更新しましたか？",
                        MessageTitle,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.Yes)
                {
                    this.Enabled = true;
                    return;
                }

                // DBオープン
                cn.Open();

                // 1.ファイル選択ダイアログの表示
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "一括取り込みするファイルを選択してください。";
                ofd.Filter = "jsonファイル(*.json)|*.json;";
                ofd.Multiselect = false;
                //   ダイアログを表示する
                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    // キャンセル押下時は何もしない
                    this.Enabled = true;
                    return;
                }

                // 読み込みファイルのフォルダパスを取得(画像ファイルの置場)
                folderPath = System.IO.Path.GetDirectoryName(ofd.FileName);

                // 2.最大ID取得
                cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT MAX(Id) AS MaxId FROM PictureBook";
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    iId = int.Parse(reader["MaxId"].ToString());
                }

                // 3.同カテゴリ内の最大ソートNo取得
                cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT MAX(SortNumber) AS MaxSortNumber FROM PictureBook WHERE ParentId = @ParentID";
                cmd.Parameters.Add("ParentID", System.Data.DbType.Int64);
                cmd.Parameters["ParentID"].Value = m_ParentID;
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    if (reader["MaxSortNumber"] is System.DBNull)
                    {
                        iSortNumber = 0;
                    }
                    else
                    {
                        iSortNumber = int.Parse(reader["MaxSortNumber"].ToString()) + 1;
                    }
                }

                // 4.選択ファイルの読み込み
                StreamReader sr = new StreamReader(ofd.FileName, Encoding.GetEncoding("Shift_JIS"));
                string jsonString = sr.ReadToEnd();
                sr.Close();
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(JsonData[]));
                var jsonBytes = Encoding.Unicode.GetBytes(jsonString);
                MemoryStream ms = new MemoryStream(jsonBytes);
                JsonData[] jsonDataList = (JsonData[])jsonSerializer.ReadObject(ms);
                // トランザクション
                using (SQLiteTransaction trans = cn.BeginTransaction())
                {
                    cmd = cn.CreateCommand();
                    // INSERT文のセット
                    cmd.CommandText = "INSERT INTO PictureBook"
                                    + " (Title, ParentId, Thumbnail, DetailFlag, Image, Detail, SortNumber)"
                                    + " VALUES (@Title, @ParentId, @Thumbnail, 1, @Image, @Detail, @SortNumber)";
                    cmd.Parameters.Add("Title", System.Data.DbType.String);
                    cmd.Parameters.Add("ParentId", System.Data.DbType.Int64);
                    cmd.Parameters.Add("Thumbnail", System.Data.DbType.String);
                    cmd.Parameters.Add("Image", System.Data.DbType.String);
                    cmd.Parameters.Add("Detail", System.Data.DbType.String);
                    cmd.Parameters.Add("SortNumber", System.Data.DbType.Int64);

                    // 取り込みデータ件数分ループ
                    foreach (JsonData jsonData in jsonDataList)
                    {
                        // [ID](自動採番のため設定はしないが使用はするので、ここでインクリメントしておく)
                        iId++;
                        // [タイトル]
                        cmd.Parameters["Title"].Value = jsonData.Title;
                        // [親ID]
                        cmd.Parameters["ParentId"].Value = m_ParentID;
                        // [サムネイル]
                        localFile = folderPath + "/" + jsonData.Thumbnail;
                        uploadFile = "images/" + GetImageFolder(iId) + "/" + iId + "_tmb.jpg";
                        //  FTPアップロード
                        ftpUploadFile(localFile, ftpServerUrl + uploadFile, false);
                        //  アップロードした画像ファイル名を登録
                        cmd.Parameters["Thumbnail"].Value = uploadFile;
                        // [画像ファイル]
                        iDetailIndex = 0;
                        insertImageString = "";
                        //  ","で分割
                        string[] stArrayData = jsonData.Image.Split(',');
                        foreach (string stData in stArrayData)
                        {
                            iDetailIndex++;
                            localFile = folderPath + "/" + stData;
                            uploadFile = "images/" + GetImageFolder(iId) + "/" + iId + "_detail_" + (iDetailIndex) + ".jpg";
                            //  FTPアップロード
                            ftpUploadFile(localFile, ftpServerUrl + uploadFile, false);
                            //  アップロードファイル名連結
                            insertImageString += uploadFile + ",";
                        }
                        //  最後の","を外して、FTPにアップロードした画像ファイル名を登録
                        cmd.Parameters["Image"].Value = insertImageString.Remove(insertImageString.Length - 1);
                        // [詳細]
                        cmd.Parameters["Detail"].Value = jsonData.Detail;
                        // [ソートNo]
                        cmd.Parameters["SortNumber"].Value = iSortNumber;
                        iSortNumber += 1;

                        // DBに登録
                        cmd.ExecuteNonQuery();
                    }
                    trans.Commit();
                }

                // ワークDBを正に
                File.Copy(Application.StartupPath + @"\0000000000000001_work.db",
                          Application.StartupPath + @"\0000000000000001.db", true);
                MessageBox.Show("データ一括取込が完了しました。",
                            MessageTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                // 画面再描画
                this.Enabled = true;
                SetTreeView();
            }
            catch (System.Exception ex)
            {
                //すべての例外をキャッチする
                //例外の説明を表示する
                System.Console.WriteLine(ex.Message);
                MessageBox.Show(ex.Message, MessageTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Enabled = true;
            }
        }
    }

    // 分類データクラス
    public class ClassData {
        private bool isSubFoldersAdded = false;
        private bool isChanged = false;
        private string tmbUrl = "";
        private string detailFlag = "";
        private string imageUrl = "";
        private string detail = "";

        public ClassData() {
        }

        // サブフォルダセット済みフラグ
        public bool IsFoldersAdded {
            set {
                isSubFoldersAdded = value;
            }

            get {
                return isSubFoldersAdded;
            }
        }

        // 変更されたかのフラグ
        public bool IsChanged {
            set {
                isChanged = value;
            }

            get {
                return isChanged;
            }
        }

        // サムネイルURL
        public string TmbUrl {
            set {
                tmbUrl = value;
            }

            get {
                return tmbUrl;
            }
        }

        // 詳細フラグ
        public string DetailFlag {
            set {
                detailFlag = value;
            }

            get {
                return detailFlag;
            }
        }

        // 詳細画像URL
        public string ImageUrl {
            set {
                imageUrl = value;
            }

            get {
                return imageUrl;
            }
        }

        // 詳細
        public string Detail {
            set {
                detail = value;
            }

            get {
                return detail;
            }
        }
    }

    // jsonデータクラス
    public class JsonData
    {
        private String m_Title = "";
        private String m_Detail = "";
        private String m_Thumbnail = "";
        private String m_Image = "";

        // タイトル
        public string Title{
            set 
            {
                m_Title = value;
            }

            get 
            {
                return m_Title;
            }
        }

        // 詳細
        public string Detail
        {
            set
            {
                m_Detail = value;
            }

            get
            {
                return m_Detail;
            }
        }

        // サムネイル
        public string Thumbnail
        {
            set
            {
                m_Thumbnail = value;
            }

            get
            {
                return m_Thumbnail;
            }
        }

        // 画像
        public string Image
        {
            set
            {
                m_Image = value;
            }

            get
            {
                return m_Image;
            }
        }

    }
}
