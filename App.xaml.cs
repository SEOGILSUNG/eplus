using eMBTI.Services;
using Microsoft.Maui.Controls.Shapes;

namespace eMBTI
{
    public partial class App : Application
    {
        public App()
        {
            AppExceptionHandler.Initialize();

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                _ = AppExceptionHandler.LogAsync(ex, "앱 초기화");
                Resources = new ResourceDictionary();
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                return new Window(new AppShell());
            }
            catch (Exception ex)
            {
                _ = AppExceptionHandler.LogAsync(ex, "화면 생성");
                return new Window(new ContentPage
                {
                    BackgroundColor = Color.FromArgb("#F8FAFC"),
                    Content = new Border
                    {
                        Margin = new Thickness(24),
                        Padding = new Thickness(22),
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#E2E8F0"),
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
                        VerticalOptions = LayoutOptions.Center,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label
                                {
                                    Text = "성격어때",
                                    FontSize = 26,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#1E293B"),
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                new Label
                                {
                                    Text = "앱 화면을 여는 중 문제가 발생했습니다. 앱을 종료하지 않고 안내 화면으로 전환했습니다. 다시 실행하거나 업데이트 후 이용해 주세요.",
                                    FontSize = 15,
                                    TextColor = Color.FromArgb("#475569"),
                                    LineBreakMode = LineBreakMode.WordWrap,
                                    HorizontalTextAlignment = TextAlignment.Center
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}