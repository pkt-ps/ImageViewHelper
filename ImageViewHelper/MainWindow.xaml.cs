using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Configuration;

namespace ImageViewHelper
{
	public static class ZIndex
	{
		public const int TouchInner = 0;
		public const int DefaultImage = 1;
		public const int FocusImage = 2;
		public const int Fade = 3;
		public const int ShowupImage = 4;
	}

	public class ImageContextMenu : ContextMenu
	{
		public ImageContextMenu(ImageInfo image_info, bool is_showup)
		{
			Action<string, Action> add_menu = (name, callback) =>
			{
				MenuItem item = new MenuItem();
				item.Header = name;
				item.Click += (object sender, RoutedEventArgs e) => { callback(); };
				Items.Add(item);
			};

			string fullpath = System.IO.Path.GetFullPath(image_info.m_name);

			add_menu("既定アプリで開く", ()=>
			{
				try
				{
					System.Diagnostics.Process.Start(fullpath);
				}
				catch (System.ComponentModel.Win32Exception /*e*/)
				{
					MessageBox.Show("ファイル形式が関連付けされていません");
				}
			});
			if (!is_showup)
			{
				add_menu("削除", ()=>{ MainWindow.Get.DeleteImage(image_info); });
			}
			add_menu("画像パスコピー", ()=>{ Clipboard.SetText(fullpath); });
		}
	}

	public class ImageInfo
	{
		// ここらへん目標で初期スケーリングする
		public const double TargetPixelSize = 300.0;

		public bool Create(string filename)
		{
			if (!isValidFile(filename))
			{
				return false;
			}

			m_name = filename;

			try
			{
				m_bitmap = new BitmapImage();
				m_bitmap.BeginInit();
				m_bitmap.UriSource = new Uri(System.IO.Path.GetFullPath(filename));
				m_bitmap.EndInit();
				m_bitmap.Freeze();

				m_pixel_width = m_bitmap.PixelWidth;
				m_pixel_height = m_bitmap.PixelHeight;

				AdjustScale();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				return false;
			}
			return true;
		}

		// UIスレッドから呼ぶ必要があり
		public bool Setup()
		{
			try
			{
				m_image = new Image();
				m_image.VerticalAlignment = VerticalAlignment.Top;
				m_image.HorizontalAlignment = HorizontalAlignment.Left;
				m_image.Stretch = Stretch.Uniform;
				m_image.Width = m_pixel_width * m_scale;
				m_image.Height = m_pixel_height * m_scale;
				m_image.Source = m_bitmap;
				m_image.RenderTransformOrigin = new Point(0.5, 0.5);

				m_image.MouseEnter += image_MouseEnter;
				m_image.MouseLeave += image_MouseLeave;
				m_image.MouseWheel += image_MouseWheel;
				m_image.MouseLeftButtonDown += image_MouseLeftButtonDown;
				m_image.MouseRightButtonUp += image_MouseRightButtonUp;

				Panel.SetZIndex(m_image, ZIndex.DefaultImage);
				RenderOptions.SetBitmapScalingMode(m_image, BitmapScalingMode.Fant);

				// ContextMenu
				m_image.ContextMenu = new ImageContextMenu(this, false);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				return false;
			}
			return true;
		}

		public void Destroy()
		{
			m_image = null;
			m_bitmap = null;
		}

		private static bool isValidFile(string filename)
		{
			// exist?
			if (!File.Exists(filename))
			{
				return false;
			}
			//string ext = System.IO.Path.GetExtension(filename);
			//if (ext == )
			return true;
		}
		// スケールセット UIスレッド以外からは呼べない
		public void SetScale(double scale)
		{
			m_scale = scale;
			if (m_image != null)
			{
				m_image.Width = m_pixel_width * m_scale;
				m_image.Height = m_pixel_height * m_scale;
			}
		}
		// スケール調整 UIスレッド以外からは呼べない
		public void AdjustScale()
		{
			double target_pixel_size = Math.Min(Math.Min(TargetPixelSize, MainWindow.Get.m_grid_inner.ActualWidth), MainWindow.Get.m_grid_inner.ActualHeight);
			double scale = target_pixel_size / Math.Max(m_pixel_width, m_pixel_height);

			SetScale(scale);
		}
		public double GetCurrentWidth()
		{
			return m_pixel_width * m_scale;
		}
		public double GetCurrentHeight()
		{
			return m_pixel_height * m_scale;
		}

		//! マウス侵入イベント
		private void image_MouseEnter(object sender, MouseEventArgs e)
		{
			if (m_image == null)
			{
				return;
			}
			var scale_anim = new ScaleTransform();
			{
				var double_anim = new DoubleAnimation(1.0, 1.05, new Duration(TimeSpan.FromMilliseconds(100.0)));
				scale_anim.BeginAnimation(ScaleTransform.ScaleXProperty, double_anim);
				scale_anim.BeginAnimation(ScaleTransform.ScaleYProperty, double_anim);
			}
			m_image.RenderTransform = scale_anim;
			Panel.SetZIndex(m_image, ZIndex.FocusImage);
		}
		//! マウス離脱イベント
		private void image_MouseLeave(object sender, MouseEventArgs e)
		{
			if (m_image == null)
			{
				return;
			}
			var scale_anim = new ScaleTransform();
			{
				var double_anim = new DoubleAnimation(1.05, 1.0, new Duration(TimeSpan.FromMilliseconds(100.0)));
				scale_anim.BeginAnimation(ScaleTransform.ScaleXProperty, double_anim);
				scale_anim.BeginAnimation(ScaleTransform.ScaleYProperty, double_anim);
			}
			m_image.RenderTransform = scale_anim;
			Panel.SetZIndex(m_image, ZIndex.DefaultImage);
		}
		//! マウスホイールイベント
		private void image_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) == KeyStates.Down || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) == KeyStates.Down)
			{
				m_scale += (e.Delta * 0.0001);
				m_scale = Math.Min(Math.Max(m_scale, 0.1), 100.0);

				if (m_image != null)
				{
					m_image.Width = m_pixel_width * m_scale;
					m_image.Height = m_pixel_height * m_scale;
				}

				MainWindow.Get.AlignmentImage();
				e.Handled = true;
			}
		}
		private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			MainWindow.Get.ShowUpImageReserve(this);
			MainWindow.Get.DragMove();
		}
		//! マウスアップイベント
		private void image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
		{
			//MainWindow.Get.DeleteImage(this);
		}

		//BitmapSource bitmap = BitmapSource.Create(width, height, dipx, dpiy, format, palette, array, stride);
		public string m_name = "";
		public BitmapImage m_bitmap = null;
		public Image m_image = null;
		public bool m_is_show = false;
		public double m_scale = 1.0;
		public double m_pixel_width = 0.0;
		public double m_pixel_height = 0.0;
		public bool m_is_disable = false;
	}

	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		// app setting
		private const string APP_SETTING_KEY_FILE_LIST = "filelist";
		private const string APP_SETTING_KEY_WIDTH = "width";
		private const string APP_SETTING_KEY_HEIGHT = "height";

		// directory
		private const string DIRECTORY_PASTE_IMAGE = "paste";

		// static member
		// ...微妙
		static public MainWindow Get { get { return m_self; } }
		static private MainWindow m_self;

		// member
		private bool m_is_window_maximize = false;
		private List<ImageInfo> m_image_list = new List<ImageInfo>();
		private List<ImageInfo> m_delete_image_list = new List<ImageInfo>();
		private DispatcherTimer m_dispatcher_timer = new DispatcherTimer();

		private bool m_is_showup = false;
		private ImageInfo m_showup_reserve_info = null;

		public MainWindow()
		{
			m_self = this;

			// window
			InitializeComponent();
			//MouseLeftButtonDown += (sender, e) => DragMove();
			m_rect_inner_touch.MouseLeftButtonDown += (sender, e) => DragMove();
			// ImageのButtonUpが取れないので、ウィンドウ側で取る
			MouseLeftButtonUp += (sender, e) =>
			{
				ShowUpImage(false);
			};

			// show up image
			m_showup_image.RenderTransformOrigin = new Point(0.5, 0.5);
			m_showup_image.VerticalAlignment = VerticalAlignment.Center;
			m_showup_image.HorizontalAlignment = HorizontalAlignment.Center;
			m_showup_image.Stretch = Stretch.Uniform;
			RenderOptions.SetBitmapScalingMode(m_showup_image, BitmapScalingMode.Fant);
			Panel.SetZIndex(m_showup_image, ZIndex.ShowupImage);

			// fade
			Panel.SetZIndex(m_rect_fade, ZIndex.Fade);
			
			// touch
			Panel.SetZIndex(m_rect_inner_touch, ZIndex.TouchInner);

			//m_dispatcher_timer.Interval = new TimeSpan(0, 0, 0, 0, 250);
			//m_dispatcher_timer.Tick += dispatcher_timer_Tick;
			//m_dispatcher_timer.Start();
		}

		//! 画像削除
		public void DeleteImage(ImageInfo info)
		{
			// 拡大表示中は消せない
			if (m_is_showup)
			{
				return;
			}
			if (m_showup_image.Source == info.m_bitmap)
			{
				m_showup_image.Source = null;
			}
			if (m_showup_reserve_info != null && m_showup_reserve_info.m_bitmap == info.m_bitmap)
			{
				m_showup_reserve_info.m_bitmap = null;
			}
			m_grid_inner.Children.Remove(info.m_image);
			//info.Destroy();
			//m_image_list.Remove(info);
			info.m_is_disable = true;
			m_delete_image_list.Add(info);
			AlignmentImage();
		}

		//! AppConfigs読み込み
		private void loadAppConfig()
		{
			Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			{
				// width, height
				{
					var pair_width = configuration.AppSettings.Settings[APP_SETTING_KEY_WIDTH];
					var pair_height = configuration.AppSettings.Settings[APP_SETTING_KEY_HEIGHT];
					if (pair_width != null && pair_height != null)
					{
						int w, h;
						if (int.TryParse(pair_width.Value, out w) && int.TryParse(pair_height.Value, out h))
						{
							m_window.Width = w;
							m_window.Height = h;
						}
					}
				}

				// filelist
				{
					// settingから情報収集
					var pair = configuration.AppSettings.Settings[APP_SETTING_KEY_FILE_LIST];
					if (pair != null)
					{
						var fileinfo_list = pair.Value.Split(',');
						foreach (var s in fileinfo_list)
						{
							s.Trim();
						}

						string[] filelist = new string[fileinfo_list.Length / 2];
						var scale_list = new Dictionary<string, double>();
						for (int i=0; i<filelist.Length; i++)
						{
							filelist[i] = fileinfo_list[i*2 + 0];

							double scale = 1.0;
							var scale_str = fileinfo_list[i*2 + 1];
							if (double.TryParse(scale_str, out scale))
							{
								scale_list.Add(filelist[i], scale);
							}
						}
						appendImageFromFile(filelist, () =>
						{
							// 読み込み完了コールバックでスケーリング調整した後に再配置している
							// やや無理やり
							foreach (var info in m_image_list)
							{
								if (scale_list.ContainsKey(info.m_name))
								{
									info.SetScale(scale_list[info.m_name]);
								}
							}
							AlignmentImage();
						});
					}
				}
			}
		}

		//! AppConfigs保存
		private void saveAppConfig()
		{
			Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			{
				configuration.AppSettings.Settings.Clear();
				// width, height
				{
					configuration.AppSettings.Settings.Add(APP_SETTING_KEY_WIDTH, ((int)m_window.ActualWidth).ToString());
					configuration.AppSettings.Settings.Add(APP_SETTING_KEY_HEIGHT, ((int)m_window.ActualHeight).ToString());
				}
				// filelist
				{
					// settingから情報収集
					var pair = configuration.AppSettings.Settings[APP_SETTING_KEY_FILE_LIST];
					if (pair == null)
					{
						configuration.AppSettings.Settings.Add(APP_SETTING_KEY_FILE_LIST, "");
						pair = configuration.AppSettings.Settings[APP_SETTING_KEY_FILE_LIST];
					}
					// 現在の状態を保存
					StringBuilder sb = new StringBuilder();
					//foreach (ImageInfo info in m_image_list)
					for (int i=0; i<m_image_list.Count; i++)
					{
						if (m_image_list[i].m_is_disable)
						{
							continue;
						}
						sb.Append(m_image_list[i].m_name);	// filename
						sb.Append(",");
						sb.Append(m_image_list[i].m_scale.ToString()); // scale
						if (i != m_image_list.Count-1)
						{
							sb.Append(",");
						}
					}
					pair.Value = sb.ToString();
				}
				configuration.Save();
			}
		}

		//! 画像追加
		private async void appendImageFromFile(string[] filenames, Action callback = null)
		{
			await Task.Run(() =>
			{
				foreach (var s in filenames)
				{
					if (s.Length <= 0)
					{
						continue;
					}
					// 表示可能なデータかチェック
					ImageInfo info = new ImageInfo();
					if (info.Create(s))
					{
						// リスト追加
						m_image_list.Add(info);
					}
					else
					{
						if (File.Exists(s))
						{
							MessageBox.Show("表示不能な画像データ:" + System.IO.Path.GetFileName(s));
						}
						else
						{
							MessageBox.Show("ファイルが存在しません:" + System.IO.Path.GetFileName(s));
						}
					}
				}
				this.Dispatcher.InvokeAsync((Action)(() =>
				{
					// コントロール追加
					foreach (var info in m_image_list)
					{
						if (info.m_is_show)
						{
							continue;
						}
						info.m_is_show = true;

						info.Setup();
						// 整列
						AlignmentImage(info);
						m_grid_inner.Children.Add(info.m_image);
					}

					if (callback != null)
					{
						callback();
					}
					saveAppConfig();
				}));
			});
		}

		//! 画像並び替え
		public void AlignmentImage(ImageInfo only_image = null)
		{
#if true
			var rectangles = new ValueTuple<double, double, double, double>[m_image_list.Count];
			{
				// まずは矩形情報を収集
				for(int i=0; i<m_image_list.Count; i++)
				{
					if (m_image_list[i].m_is_disable)
					{
						continue;
					}
					if (!m_image_list[i].m_is_show)
					{
						continue;
					}
					if (m_image_list[i].m_image == null)
					{
						continue;
					}
					rectangles[i] = new ValueTuple<double, double, double, double>(0.0, 0.0, m_image_list[i].GetCurrentWidth(), m_image_list[i].GetCurrentHeight());
				}
				// 矩形を並べていく
				double cur_x = 0.0;
				double window_width = m_grid_inner.ActualWidth;
				for(int i=0; i<m_image_list.Count; i++)
				{
					if (m_image_list[i].m_is_disable)
					{
						continue;
					}
					if (!m_image_list[i].m_is_show)
					{
						continue;
					}
					if (m_image_list[i].m_image == null)
					{
						continue;
					}
					ref var rect = ref rectangles[i];

					double width = rect.Item3;
					double height = rect.Item4;
					double abs_right = cur_x + width;

					if (abs_right > window_width)
					{
						// はみ出すのであれば、下にずらす
						if (cur_x == 0.0)
						{
							// ずらしても入らないのでどうする？(スケーリングする？)
						}
						cur_x = 0.0;
						abs_right = cur_x + width;
					}
					else
					{
						// はみ出さないのであれば横へ配置
					}

					// Yを正しく配置する
					double cur_y = 0.0;
					{
						// どの領域に被さっているかをチェック
						for (int j=0; j<rectangles.Length; j++)
						{
							if (j >= i)
							{
								// まだ未配置
								continue;
							}
							// cur_x ~ abs_right に配置される予定なのでそことXが被る領域のYの最大値を取る
							ref var r = ref rectangles[j];
							double rect_left = r.Item1;
							double rect_right = r.Item1 + r.Item3;
							if ((rect_left <= cur_x && cur_x < rect_right) ||				// 左側が重なっているか
								(rect_left <= abs_right && abs_right < rect_right) ||		// 右側が重なっているか
								(cur_x < rect_right && rect_left <= abs_right))				// 完全に内包しているか
							{
								double abs_y = r.Item2 + r.Item4;
								cur_y = Math.Max(abs_y, cur_y);
							}
						}
					}
					rect.Item1 = cur_x;
					rect.Item2 = cur_y;

					cur_x += width;
				}
			}
			// 画像を実際に配置
			{
				for(int i=0; i<m_image_list.Count; i++)
				{
					var image = m_image_list[i].m_image;
					if (image != null)
					{
						double left = rectangles[i].Item1;
						double top = rectangles[i].Item2;
						image.Margin = new Thickness(left, top, 0.0, 0.0);
					}
				}
			}
#else
			// 座標決定
			{
				// 左上詰め
				double left = 0.0;
				//double top = m_offset_allign_y;
				double top = 0.0;

				double highest_height = 0.0;
				foreach (var i in m_image_list)
				{
					if (!i.m_is_show)
					{
						continue;
					}

					// はみ出すのであれば、下にずらす
					if (left + i.GetCurrentWidth() > m_grid_inner.ActualWidth)
					{
						if (left == 0.0)
						{
							// ずらしても入らないのでスケーリングを弄る
							i.AdjustScale();
						}
						else
						{
							left = 0.0;
							top += highest_height;
							highest_height = 0.0;
						}
					}
					if (only_image == null || (only_image!=null && i == only_image))
					{
						i.m_image.Margin = new Thickness(left, top, 0.0, 0.0);
					}
					left += i.GetCurrentWidth();
					highest_height = Math.Max(i.GetCurrentHeight(), highest_height);
				}
			}
#endif
		}

		//! 画像強調表示予約
		public void ShowUpImageReserve(ImageInfo image)
		{
			if (image.m_bitmap != null)
			{
				m_showup_reserve_info = image;
			}
		}
		//! 画像強調表示
		public void ShowUpImage(bool force_show_up)
		{
			if (m_is_showup && !force_show_up)
			{
				m_rect_fade.Visibility = Visibility.Hidden;

				var scale_anim = new ScaleTransform();
				{
					var double_anim = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(100.0)));
					scale_anim.BeginAnimation(ScaleTransform.ScaleXProperty, double_anim);
					scale_anim.BeginAnimation(ScaleTransform.ScaleYProperty, double_anim);
				}
				m_showup_image.RenderTransform = scale_anim;
				m_showup_image.ContextMenu = null;

				m_is_showup = false;
			}
			else
			{
				if (m_showup_reserve_info != null)
				{
					m_rect_fade.Visibility = Visibility.Visible;

					double scale = m_scroll_viewer.ActualHeight / Math.Max(m_showup_reserve_info.m_bitmap.PixelWidth, m_showup_reserve_info.m_bitmap.PixelHeight);

					m_showup_image.Source = m_showup_reserve_info.m_bitmap;
					m_showup_image.Width = m_showup_reserve_info.m_bitmap.PixelWidth * scale; // 描画されない範囲はクリッピングされるらしく、レンダートランスフォームだとかけてしまうので、全体を縮小しておく
					m_showup_image.Height = m_showup_reserve_info.m_bitmap.PixelHeight * scale;

					var scale_anim = new ScaleTransform();
					{
						var double_anim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(100.0)));
						scale_anim.BeginAnimation(ScaleTransform.ScaleXProperty, double_anim);
						scale_anim.BeginAnimation(ScaleTransform.ScaleYProperty, double_anim);
					}
					m_showup_image.RenderTransform = scale_anim;
					m_showup_image.ContextMenu = new ImageContextMenu(m_showup_reserve_info, true);

					m_is_showup = true;
				}
			}
		}

		//! タイマー起動
		//private void dispatcher_timer_Tick(object sender, EventArgs e)
		//{
		//	// マウスイベントだとイベント発火の順序が想定通りに制御できないので、
		//	// 仕方なくタイマーで発火, WPFに詳しければもっと正しくできそうだけど...
		//	// -> 大丈夫になった
		//	if (m_showup_reserve_info == null)
		//	{
		//		return;
		//	}
		//	ShowUpImage();
		//	m_showup_reserve_info = null;
		//}

		//! 最小化
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			SystemCommands.MinimizeWindow(this);
		}
		//! ウィンドウ化/最大化
		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			if (m_is_window_maximize)
			{
				SystemCommands.RestoreWindow(this);
				m_button_maximize.Content = "1";
			}
			else
			{
				SystemCommands.MaximizeWindow(this);
				m_button_maximize.Content = "2";
			}
			m_is_window_maximize = !m_is_window_maximize;
		}
		//! 閉じる
		private void Button_Click_2(object sender, RoutedEventArgs e)
		{
			SystemCommands.CloseWindow(this);
		}

		//! D&D Over
		private void Window_PreviewDragOver(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
			{
				e.Effects = DragDropEffects.Copy;
			}
			else
			{
				e.Effects = DragDropEffects.None;
			}
			e.Handled = true;
		}
		//! D&D Drop
		private void Window_Drop(object sender, DragEventArgs e)
		{
			string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
			if (files != null)
			{
				appendImageFromFile(files);
			}
		}
		//! ウィンドウサイズ変更
		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			AlignmentImage(null);
		}
		//! ロード完了時
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			loadAppConfig();
		}
		//! ウィンドウ終了時
		private void Window_Closed(object sender, EventArgs e)
		{
			saveAppConfig();
		}
		//! ペースト
		private void OnCtrlV(object sender, RoutedEventArgs e)
		{
			if (Clipboard.ContainsImage())
			{
				var data = Clipboard.GetDataObject();
				if (data.GetDataPresent(DataFormats.Dib, false))
				{
					Directory.CreateDirectory(DIRECTORY_PASTE_IMAGE);
					string filename = string.Format("./{0}/{1}.png", DIRECTORY_PASTE_IMAGE, DateTime.Now.ToString("yyyyMMddhhmmssfff"));
					using (var tmp_stream = new MemoryStream())
					{
						createBitmapFromDIB(data.GetData(DataFormats.Dib, false) as MemoryStream, tmp_stream);
						using (var file_stream = new FileStream(filename, FileMode.Create))
						{
							using (var stream = new MemoryStream(tmp_stream.ToArray()))
							{
								BitmapEncoder encoder = new PngBitmapEncoder();
								encoder.Frames.Add(BitmapFrame.Create(stream));
								encoder.Save(file_stream);
							}
						}
					}

					if (File.Exists(filename))
					{
						appendImageFromFile(new string[] { filename });
					}
				}
			}
		}
		//! アンドゥ
		private void OnCtrlZ(object sender, RoutedEventArgs e)
		{
			if (m_delete_image_list.Count > 0)
			{
				var info = m_delete_image_list[0];
				info.m_is_disable = false;
				m_grid_inner.Children.Add(info.m_image);
				m_delete_image_list.Remove(info);
				AlignmentImage();
			}
		}

		void createBitmapFromDIB(MemoryStream input, Stream output)
		{
			const int BITMAP_FILE_HEADER_SIZE = 14;

			byte[] raw = input.ToArray();
			int header_size = BitConverter.ToInt32(raw, 0);
			int pixel_size = raw.Length - header_size;
			int file_size = BITMAP_FILE_HEADER_SIZE + raw.Length;

			//using (var bitmap_stream = new MemoryStream(file_size))
			//using (var bitmap_stream = new FileStream(filename, FileMode.Create))
			{
				using (var writer = new BinaryWriter(output))
				{
					writer.Write(Encoding.ASCII.GetBytes("BM"));
					writer.Write(file_size);
					writer.Write(0U);
					writer.Write(BITMAP_FILE_HEADER_SIZE + header_size);
					writer.Write(raw);
					writer.Flush();
					output.Seek(0, SeekOrigin.Begin);
				}
			}
		}

		//! ウィンドウ位置変更
		private void Window_LocationChanged(object sender, EventArgs e)
		{
			// 動かされたら拡大表示予約は取り消し
			m_showup_reserve_info = null;
		}

		// グリッドホイール
		private void grid_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			//m_offset_allign_y += Math.Max(0.0, e.Delta * 0.1);
			//double delta = e.Delta * 0.1;
			//foreach (var i in m_image_list)
			//{
			//	if (!i.m_is_show)
			//	{
			//		continue;
			//	}
			//	var margin = i.m_image.Margin;
			//	margin.Top += delta;
			//	i.m_image.Margin = margin;
			//}
		}

		private void Window_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.A || e.Key == Key.D)
			{
				bool is_left = (e.Key == Key.Left || e.Key == Key.A);

				int idx = -1;
				for (int i=0; i<m_image_list.Count; i++)
				{
					if (m_image_list[i] == m_showup_reserve_info)
					{
						idx = i;
						break;
					}
				}
				if (idx != -1)
				{
					idx += is_left ? -1 : 1;
					if (idx < 0)
					{
						idx = m_image_list.Count + idx;
					}
					else if (idx >= m_image_list.Count)
					{
						idx = (idx - m_image_list.Count);
					}
					if (0 <= idx && idx < m_image_list.Count)
					{
						ShowUpImageReserve(m_image_list[idx]);
						ShowUpImage(true);
					}
				}
			}
		}
	}
}
