using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using eMBTI.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Text;

namespace eMBTI;

public partial class MainPage : ContentPage
{
    private readonly Dictionary<string, PersonalityTest> _tests;
    private PersonalityTest? _currentTest;
    private TestResult? _currentResult;
    private string _currentResultKey = string.Empty;
    private int _selectedQuestionCount = 10;
    private readonly List<TestQuestion> _activeQuestions = new();
    private readonly Dictionary<string, int> _scores = new();
    private readonly List<string> _answerHistory = new();
    private int _currentIndex;
    private bool _compactLayout;
    private double _lastLayoutWidth;
    private TaskCompletionSource<bool>? _confirmPopupCompletionSource;

    public MainPage()
    {
        try
        {
            InitializeComponent();
            _tests = BuildTests();
            ShowMenu();
        }
        catch (Exception ex)
        {
            _tests = new Dictionary<string, PersonalityTest>();
            Content = BuildSafeFallbackContent();
            _ = AppExceptionHandler.HandleAsync(ex, "메인 화면 초기화");
        }
    }

    private static View BuildSafeFallbackContent()
    {
        return new Grid
        {
            Padding = new Thickness(24),
            BackgroundColor = Color.FromArgb("#F8FAF7"),
            Children =
            {
                new Border
                {
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
                                TextColor = Color.FromArgb("#27364A"),
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = "화면을 구성하는 중 오류가 발생했습니다. 앱은 종료되지 않도록 안전 화면을 표시했습니다.",
                                FontSize = 15,
                                TextColor = Color.FromArgb("#475569"),
                                LineBreakMode = LineBreakMode.WordWrap,
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        }
                    }
                }
            }
        };
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        try
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0 || Math.Abs(width - _lastLayoutWidth) < 12)
                return;

            _lastLayoutWidth = width;

            bool compact = width < 340;
            bool tablet = width >= 700;

            RootGrid.Padding = compact ? new Thickness(10, 10, 10, 14) : new Thickness(18, 16, 18, 20);

            double horizontalPadding = compact ? 20 : 36;
            double contentWidth = Math.Max(280, width - horizontalPadding);
            MainContent.WidthRequest = tablet ? Math.Min(contentWidth, 760) : contentWidth;
            MainContent.MaximumWidthRequest = tablet ? 760 : 9999;

            if (compact != _compactLayout)
            {
                _compactLayout = compact;
                ApplyAdaptiveGridLayout();
            }
        }
        catch (Exception ex)
        {
            _ = AppExceptionHandler.LogAsync(ex, "화면 크기 조정");
        }
    }

    private void ApplyAdaptiveGridLayout()
    {
        // 일반 폰에서도 첫 화면이 한 화면 안에 보이도록 기본 2열을 유지합니다.
        if (_compactLayout)
        {
            SetGridColumns(MenuGrid, 2);
            SetGridColumns(CountGrid, 2);
            SetGridColumns(ResultButtonGrid, 2);
            SetGridColumns(QuestionNavGrid, 3);
            SetGridColumns(DetailButtonGrid, 3);
        }
        else
        {
            SetGridColumns(MenuGrid, 2);
            SetGridColumns(CountGrid, 2);
            SetGridColumns(ResultButtonGrid, 2);
            SetGridColumns(QuestionNavGrid, 3);
            SetGridColumns(DetailButtonGrid, 3);
        }
    }

    private static void SetGridColumns(Grid grid, int columns)
    {
        if (grid == null || columns < 1)
            return;

        var children = grid.Children.OfType<View>().ToList();

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        for (int i = 0; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        int rows = (int)Math.Ceiling(children.Count / (double)columns);
        for (int i = 0; i < rows; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < children.Count; i++)
        {
            Grid.SetRow(children[i], i / columns);
            Grid.SetColumn(children[i], i % columns);
        }
    }


    private async void HelpClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(async () =>
        {
            if (sender is not Button button)
                return;

            if (!string.IsNullOrWhiteSpace(button.StyleId))
            {
                var url = GetHelpUrl(button.StyleId);
                if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
        }, "도움말 열기");
    }

    private static string GetHelpUrl(string testKey) => testKey switch
    {
        "mbti" => "https://posbar.tistory.com/399",
        "animal" => "https://posbar.tistory.com/441",
        "love" => "https://posbar.tistory.com/442",
        "work" => "https://posbar.tistory.com/443",
        "money" => "https://posbar.tistory.com/444",
        "travel" => "https://posbar.tistory.com/445",
        "food" => "https://posbar.tistory.com/446",
        "color" => "https://posbar.tistory.com/447",
        _ => string.Empty
    };

    private static string GetShareTestTypeName(PersonalityTest test) => test.Key switch
    {
        "mbti" => "MBTI 성격유형",
        "animal" => "동물 성격유형",
        "love" => "연애 성향 테스트",
        "work" => "직장인 업무유형",
        "money" => "소비 성향 테스트",
        "travel" => "여행 스타일 테스트",
        "food" => "음식 취향 성격",
        "color" => "색깔 성격유형",
        _ => test.Title
    };

    private async void MbtiMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("mbti"), "MBTI 테스트 시작");
    private async void AnimalMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("animal"), "동물 테스트 시작");
    private async void LoveMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("love"), "연애 테스트 시작");
    private async void WorkMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("work"), "직장 테스트 시작");
    private async void MoneyMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("money"), "소비 테스트 시작");
    private async void TravelMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("travel"), "여행 테스트 시작");
    private async void FoodMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("food"), "음식 테스트 시작");
    private async void ColorMenuClicked(object sender, EventArgs e) => await AppExceptionHandler.RunAsync(() => StartTest("color"), "색깔 테스트 시작");

    private void StartTest(string testKey)
    {
        if (!_tests.TryGetValue(testKey, out var test))
            return;

        _currentTest = test;
        _currentResult = null;
        _currentResultKey = string.Empty;

        BrandHeader.IsVisible = false;
        MenuView.IsVisible = false;
        QuestionView.IsVisible = false;
        ResultView.IsVisible = false;
        DetailView.IsVisible = false;
        CountView.IsVisible = true;

        CountTitleLabel.Text = $"{test.Icon} {test.Title} - 문항 수 선택";
    }

    private async void QuestionCountClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            if (sender is not Button button || !int.TryParse(button.StyleId, out int count))
                count = 10;

            BeginTest(count);
        }, "문항 수 선택");
    }

    private void BeginTest(int questionCount)
    {
        if (_currentTest == null)
            return;

        _selectedQuestionCount = questionCount;
        _scores.Clear();
        _answerHistory.Clear();
        _activeQuestions.Clear();
        _currentIndex = 0;
        _currentResult = null;
        _currentResultKey = string.Empty;

        foreach (var result in _currentTest.Results)
            _scores[result.Key] = 0;

        _activeQuestions.AddRange(BuildQuestionSet(_currentTest, _selectedQuestionCount));

        if (_activeQuestions.Count == 0)
        {
            ShowMenu();
            _ = AppExceptionHandler.HandleAsync(new InvalidOperationException("표시할 문항이 없습니다."), "검사 시작");
            return;
        }

        BrandHeader.IsVisible = false;
        MenuView.IsVisible = false;
        CountView.IsVisible = false;
        ResultView.IsVisible = false;
        DetailView.IsVisible = false;
        QuestionView.IsVisible = true;

        QuestionIconLabel.Text = _currentTest.Icon;
        QuestionTestTitleLabel.Text = $"{_currentTest.Title} · {_selectedQuestionCount}문항";
        QuestionBadgeLabel.Text = _currentTest.Badge;
        ResultHeaderLabel.Text = _currentTest.Title + " 결과";

        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (_currentTest == null)
            return;

        if (_currentIndex >= _activeQuestions.Count)
        {
            ShowResult();
            return;
        }

        var question = _activeQuestions[_currentIndex];
        QuestionTitle.Text = question.Text;
        ProgressText.Text = $"{_currentIndex + 1} / {_activeQuestions.Count}";
        QuestionProgress.Progress = _activeQuestions.Count == 0 ? 0 : (double)_currentIndex / _activeQuestions.Count;

        PrevButton.IsEnabled = _currentIndex > 0;
        PrevButton.Opacity = _currentIndex > 0 ? 1.0 : 0.45;

        OptionContainer.Children.Clear();
        foreach (var option in question.Options)
        {
            var button = new Button
            {
                Text = option.Text,
                StyleId = $"{option.ScoreKey}|{option.Score}",
                MinimumHeightRequest = 50,
                CornerRadius = 15,
                Padding = new Thickness(12, 8),
                FontSize = 14,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                BorderColor = Color.FromArgb("#D7DEE8"),
                BorderWidth = 1,
                TextColor = Color.FromArgb("#27364A")
            };
            button.Clicked += OptionClicked;
            OptionContainer.Children.Add(button);
        }
    }

    private async void OptionClicked(object? sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            if (sender is not Button button || string.IsNullOrWhiteSpace(button.StyleId))
                return;

            button.IsEnabled = false;

            var parts = button.StyleId.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            string scoreKey = parts[0];
            int weight = parts.Length > 1 && int.TryParse(parts[1], out int parsedWeight) ? parsedWeight : 1;

            if (!_scores.ContainsKey(scoreKey))
                _scores[scoreKey] = 0;

            _scores[scoreKey] += weight;
            _answerHistory.Add($"{scoreKey}|{weight}");
            _currentIndex++;
            ShowQuestion();
        }, "답변 선택");
    }

    private async void PrevClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            if (_currentIndex <= 0 || _answerHistory.Count == 0)
                return;

            var parts = _answerHistory[^1].Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            string lastKey = parts[0];
            int weight = parts.Length > 1 && int.TryParse(parts[1], out int parsedWeight) ? parsedWeight : 1;
            _answerHistory.RemoveAt(_answerHistory.Count - 1);
            if (_scores.ContainsKey(lastKey))
                _scores[lastKey] = Math.Max(0, _scores[lastKey] - weight);

            _currentIndex--;
            ShowQuestion();
        }, "이전 문항 이동");
    }

    private void ShowResult()
    {
        if (_currentTest == null)
            return;

        if (_currentTest.Results.Count == 0)
        {
            ShowMenu();
            _ = AppExceptionHandler.HandleAsync(new InvalidOperationException("테스트 결과 데이터가 없습니다."), "결과 계산");
            return;
        }

        string resultKey = _currentTest.Key == "mbti"
            ? BuildMbtiResult()
            : (_scores.Count == 0 ? _currentTest.Results.Keys.First() : _scores.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key);

        if (!_currentTest.Results.TryGetValue(resultKey, out var result))
            result = _currentTest.Results.Values.First();

        _currentResultKey = resultKey;
        _currentResult = result;

        ResultEmojiLabel.Text = result.Emoji;
        ResultTypeLabel.Text = result.Title;
        ResultDescLabel.Text = result.Description;
        ResultAdviceLabel.Text = result.Advice;
        ScoreSummaryLabel.Text = BuildScoreSummary(_currentTest);

        QuestionProgress.Progress = 1;
        BrandHeader.IsVisible = false;
        MenuView.IsVisible = false;
        CountView.IsVisible = false;
        QuestionView.IsVisible = false;
        DetailView.IsVisible = false;
        ResultView.IsVisible = true;
    }

    private string BuildMbtiResult()
    {
        string ei = GetScore("E") >= GetScore("I") ? "E" : "I";
        string sn = GetScore("N") >= GetScore("S") ? "N" : "S";
        string tf = GetScore("F") >= GetScore("T") ? "F" : "T";
        string jp = GetScore("P") >= GetScore("J") ? "P" : "J";
        return ei + sn + tf + jp;
    }

    private int GetScore(string key) => _scores.TryGetValue(key, out int value) ? value : 0;

    private string BuildScoreSummary(PersonalityTest test)
    {
        if (test.Key == "mbti")
            return $"E {GetScore("E")} / I {GetScore("I")} · S {GetScore("S")} / N {GetScore("N")} · T {GetScore("T")} / F {GetScore("F")} · J {GetScore("J")} / P {GetScore("P")}";

        return string.Join(" · ", test.Results.Keys.Select(k => $"{test.Results[k].ShortName} {GetScore(k)}"));
    }

    private async void RetryClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            if (_currentTest == null)
            {
                ShowMenu();
                return;
            }

            BeginTest(_selectedQuestionCount);
        }, "다시 검사");
    }

    private async void HomeClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(async () =>
        {
            if (QuestionView.IsVisible && _currentIndex > 0)
            {
                bool ok = await ShowConfirmPopupAsync(
                    "메뉴로 이동",
                    "현재 진행 중인 테스트를 멈추고 메뉴로 이동할까요?",
                    "진행 중인 답변은 초기화됩니다.",
                    "메뉴로",
                    "계속하기",
                    "🏠",
                    Color.FromArgb("#F3E8C9"),
                    Color.FromArgb("#B68A32"));

                if (!ok)
                    return;
            }

            ShowMenu();
        }, "메뉴 이동");
    }

    private void ShowMenu()
    {
        BrandHeader.IsVisible = true;
        MenuView.IsVisible = true;
        CountView.IsVisible = false;
        QuestionView.IsVisible = false;
        ResultView.IsVisible = false;
        DetailView.IsVisible = false;
        QuestionProgress.Progress = 0;
        ProgressText.Text = string.Empty;
    }

    private async void DetailClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            if (_currentTest == null || _currentResult == null)
                return;

            DetailHeaderLabel.Text = $"{_currentTest.Title} 상세분석";
            DetailTitleLabel.Text = $"{_currentResult.Emoji} {_currentResult.Title}";
            DetailBodyLabel.Text = BuildDetailText(_currentTest, _currentResultKey, _currentResult);

            BrandHeader.IsVisible = false;
            MenuView.IsVisible = false;
            CountView.IsVisible = false;
            QuestionView.IsVisible = false;
            ResultView.IsVisible = false;
            DetailView.IsVisible = true;
        }, "상세분석 보기");
    }

    private async void BackToResultClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            DetailView.IsVisible = false;
            ResultView.IsVisible = true;
        }, "결과 화면 이동");
    }

    private async void ShareClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(async () =>
        {
            if (_currentTest == null || _currentResult == null)
                return;

            bool isDetailShare = DetailView.IsVisible;
            string text = isDetailShare ? BuildDetailShareText() : BuildResultShareText();
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = isDetailShare ? "성격어때 상세분석 공유" : "성격어때 결과 공유",
                Text = text
            });
        }, "결과 공유");
    }

    private string BuildResultShareText()
    {
        if (_currentTest == null || _currentResult == null)
            return string.Empty;

        return $"[성격어때 결과]\n" +
               $"테스트: {_currentTest.Title}\n" +
               $"문항 수: {_selectedQuestionCount}문항\n" +
               $"결과: {_currentResult.Emoji} {_currentResult.Title}\n\n" +
               $"{_currentResult.Description}\n\n" +
               $"조언: {_currentResult.Advice}\n\n" +
               $"점수: {BuildScoreSummary(_currentTest)}\n\n" +
               BuildShareFooter(_currentTest);
    }

    private string BuildDetailShareText()
    {
        if (_currentTest == null || _currentResult == null)
            return string.Empty;

        string detailText = BuildDetailText(_currentTest, _currentResultKey, _currentResult);

        return $"[성격어때 상세분석]\n" +
               $"테스트: {_currentTest.Title}\n" +
               $"문항 수: {_selectedQuestionCount}문항\n" +
               $"결과: {_currentResult.Emoji} {_currentResult.Title}\n\n" +
               $"{detailText}\n" +
               BuildShareFooter(_currentTest);
    }

    private static string BuildShareFooter(PersonalityTest test)
    {
        string url = GetHelpUrl(test.Key);
        string typeName = GetShareTestTypeName(test);

        if (string.IsNullOrWhiteSpace(url))
            return $"검사유형 : {typeName}";

        return $"검사유형 : {typeName}\n티스토리 : {url}";
    }

    private static List<TestQuestion> BuildQuestionSet(PersonalityTest test, int count)
    {
        if (test.Questions.Count == 0)
            return new List<TestQuestion>();

        int safeCount = Math.Clamp(count, 1, test.Questions.Count);

        return test.Questions
            .Take(safeCount)
            .Where(q => q.Options.Count > 0)
            .Select(q => new TestQuestion(q.Text, q.Options))
            .ToList();
    }

    private string BuildDetailText(PersonalityTest test, string resultKey, TestResult result)
    {
        if (test.Key == "animal")
            return BuildAnimalDetail(resultKey, result);

        var sb = new StringBuilder();
        sb.AppendLine(result.Description);
        sb.AppendLine();
        sb.AppendLine("■ 주요 특징");
        sb.AppendLine(GetGenericFeatures(test.Key, result.ShortName));
        sb.AppendLine();
        sb.AppendLine("■ 강점");
        sb.AppendLine(GetGenericStrength(test.Key));
        sb.AppendLine();
        sb.AppendLine("■ 주의할 점");
        sb.AppendLine(result.Advice);
        sb.AppendLine();
        sb.AppendLine("■ 점수 요약");
        sb.AppendLine(BuildScoreSummary(test));
        return sb.ToString();
    }

    private static string BuildAnimalDetail(string resultKey, TestResult result)
    {
        return resultKey switch
        {
            "dog" => "밝고 친근한 사교형입니다. 사람들과 어울릴 때 에너지를 얻고 주변 분위기를 밝게 만듭니다.\n\n■ 주요 특징\n친화력이 좋고, 사람을 잘 챙기며, 감정 표현이 자연스럽습니다. 팀 활동에 잘 어울리고 함께 있는 사람에게 편안함을 줍니다.\n\n■ 장점\n따뜻하고 다정한 성격으로 관계를 부드럽게 만들며 긍정적인 에너지를 전달합니다.\n\n■ 주의할 점\n타인의 반응에 너무 민감해질 수 있습니다. 가끔은 혼자만의 시간도 필요합니다.\n\n■ 잘 맞는 유형\n토끼형, 돌고래형, 사자형",
            "cat" => "독립적이고 섬세한 자유형입니다. 혼자만의 시간과 자유를 중요하게 생각하며 자신만의 취향과 기준이 분명합니다.\n\n■ 주요 특징\n독립심이 강하고 관찰력이 좋으며 감정을 쉽게 드러내지 않습니다.\n\n■ 장점\n감정에 쉽게 휘둘리지 않고 차분하게 상황을 바라보는 힘이 있습니다.\n\n■ 주의할 점\n차갑거나 무심해 보일 수 있습니다. 가까운 사람에게는 마음을 조금 더 표현해보는 것이 좋습니다.\n\n■ 잘 맞는 유형\n부엉이형, 여우형, 토끼형",
            "lion" => "추진력 강한 리더형입니다. 목표가 생기면 빠르게 움직이고 어려운 상황에서도 앞장서는 편입니다.\n\n■ 주요 특징\n결단력, 책임감, 리더십, 목표 지향성이 강합니다.\n\n■ 장점\n주변 사람들에게 든든한 인상을 주고 일을 실제 결과로 만드는 힘이 있습니다.\n\n■ 주의할 점\n자기주장이 강하게 보일 수 있습니다. 상대방의 속도와 감정도 함께 살피면 더 좋은 리더가 됩니다.\n\n■ 잘 맞는 유형\n강아지형, 부엉이형, 돌고래형",
            "rabbit" => "배려 깊고 조심스러운 안정형입니다. 갈등을 싫어하고 주변 사람들과 편안한 관계를 유지하려고 노력합니다.\n\n■ 주요 특징\n배려심이 많고 상대의 감정을 잘 살피며 조용한 안정감을 줍니다.\n\n■ 장점\n말과 행동이 부드러워 주변 사람을 편안하게 만들고 신뢰를 쌓습니다.\n\n■ 주의할 점\n자기 생각을 너무 참을 수 있습니다. 필요할 때는 의견을 분명히 표현해보세요.\n\n■ 잘 맞는 유형\n강아지형, 고양이형, 돌고래형",
            "owl" => "분석적이고 신중한 지성형입니다. 즉흥적인 행동보다 충분히 생각하고 판단하는 것을 선호합니다.\n\n■ 주요 특징\n관찰력과 분석력이 좋고, 논리와 근거를 중요하게 생각합니다.\n\n■ 장점\n복잡한 상황에서도 차분하게 판단하고 문제를 구조적으로 해결합니다.\n\n■ 주의할 점\n생각이 많아 행동이 늦어질 수 있습니다. 완벽한 준비보다 적절한 실행이 필요할 때도 있습니다.\n\n■ 잘 맞는 유형\n고양이형, 사자형, 여우형",
            "fox" => "눈치 빠른 전략형입니다. 사람의 말투, 분위기, 흐름을 잘 읽고 상황에 맞게 행동합니다.\n\n■ 주요 특징\n상황 판단이 빠르고 센스가 좋으며 변화에 잘 적응합니다.\n\n■ 장점\n다양한 관계와 환경에서 유연하게 행동하고 현실적인 판단을 잘합니다.\n\n■ 주의할 점\n속마음을 잘 드러내지 않아 계산적으로 보일 수 있습니다. 가까운 관계에서는 솔직함도 중요합니다.\n\n■ 잘 맞는 유형\n고양이형, 부엉이형, 사자형",
            "dolphin" => "밝고 유연한 소통형입니다. 사람들과 대화하는 것을 좋아하고 분위기에 맞춰 자연스럽게 행동합니다.\n\n■ 주요 특징\n소통 능력이 좋고 유머 감각이 있으며 긍정적인 에너지를 전달합니다.\n\n■ 장점\n사람들과 자연스럽게 어울리고 딱딱한 분위기를 부드럽게 바꾸는 힘이 있습니다.\n\n■ 주의할 점\n즉흥적인 선택이 많아질 수 있습니다. 중요한 일은 작은 계획을 세우면 더 좋습니다.\n\n■ 잘 맞는 유형\n강아지형, 토끼형, 사자형",
            "turtle" => "꾸준하고 성실한 안정형입니다. 빠르지는 않지만 한 번 맡은 일은 책임감 있게 끝까지 해냅니다.\n\n■ 주요 특징\n성실하고 책임감이 강하며 쉽게 흔들리지 않는 안정감을 줍니다.\n\n■ 장점\n차근차근 쌓아가는 일에 강하고 주변 사람에게 믿음을 줍니다.\n\n■ 주의할 점\n변화에 적응하는 데 시간이 걸릴 수 있습니다. 작은 시도부터 시작하면 부담이 줄어듭니다.\n\n■ 잘 맞는 유형\n토끼형, 부엉이형, 강아지형",
            _ => $"{result.Description}\n\n■ 주의할 점\n{result.Advice}"
        };
    }

    private static string GetGenericFeatures(string testKey, string shortName)
    {
        return testKey switch
        {
            "mbti" => "에너지 방향, 정보 인식, 판단 방식, 생활 양식의 조합으로 현재 성향을 보여줍니다.",
            "love" => "관계에서 중요하게 생각하는 속도, 표현 방식, 안정감, 자유도를 보여줍니다.",
            "work" => "업무를 처리하는 기준, 협업 방식, 문제 해결 패턴을 보여줍니다.",
            "money" => "소비 전 판단 기준, 예산 관리 방식, 만족을 느끼는 지점을 보여줍니다.",
            "travel" => "여행에서 중요하게 생각하는 계획성, 자유도, 휴식, 경험 욕구를 보여줍니다.",
            "food" => "음식을 선택하는 기준과 취향, 안정 추구 또는 도전 성향을 보여줍니다.",
            "color" => "색이 주는 이미지로 에너지, 안정감, 감성, 조화 성향을 보여줍니다.",
            _ => $"{shortName} 성향이 강하게 나타났습니다."
        };
    }

    private static string GetGenericStrength(string testKey)
    {
        return testKey switch
        {
            "mbti" => "자신의 의사결정 방식과 대인관계 패턴을 이해하는 데 도움이 됩니다.",
            "love" => "나의 연애 표현 방식과 상대에게 필요한 배려 포인트를 확인할 수 있습니다.",
            "work" => "업무 배치, 협업, 보고 방식, 갈등 조율에 참고할 수 있습니다.",
            "money" => "소비 습관을 돌아보고 예산 관리 방향을 잡는 데 도움이 됩니다.",
            "travel" => "동행자와 여행 스타일을 맞추고 만족도 높은 일정을 짜는 데 도움이 됩니다.",
            "food" => "외식 선택, 취향 공유, 추천 콘텐츠 구성에 활용하기 좋습니다.",
            "color" => "가볍고 직관적인 방식으로 현재 감성과 성향을 표현할 수 있습니다.",
            _ => "자기이해와 관계 이해에 참고할 수 있습니다."
        };
    }

    private async void ExitClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(async () =>
        {
            bool ok = await ShowConfirmPopupAsync(
                "앱 종료",
                "성격어때를 종료할까요?",
                "검사 중이라면 현재 진행 내용은 저장되지 않습니다.",
                "종료",
                "취소",
                "👋",
                Color.FromArgb("#F6DDDD"),
                Color.FromArgb("#B46A6A"));

            if (ok)
                Application.Current?.Quit();
        }, "앱 종료");
    }

    private async Task<bool> ShowConfirmPopupAsync(
        string title,
        string message,
        string notice,
        string primaryText,
        string secondaryText,
        string icon,
        Color iconBackground,
        Color primaryColor)
    {
        _confirmPopupCompletionSource = new TaskCompletionSource<bool>();

        ConfirmPopupTitleLabel.Text = title;
        ConfirmPopupMessageLabel.Text = message;
        ConfirmPopupNoticeLabel.Text = notice;
        ConfirmPopupIconLabel.Text = icon;
        ConfirmPopupSecondaryButton.Text = secondaryText;
        ConfirmPopupPrimaryButton.Text = primaryText;

        ConfirmPopupNoticeBorder.BackgroundColor = iconBackground.WithAlpha(0.38f);
        ConfirmPopupNoticeBorder.Stroke = iconBackground;
        ConfirmPopupPrimaryButton.BackgroundColor = primaryColor;
        ConfirmPopupOverlay.Opacity = 0;
        ConfirmPopupOverlay.IsVisible = true;

        await ConfirmPopupOverlay.FadeTo(1, 120, Easing.CubicOut);
        await ConfirmPopupCard.ScaleTo(1.02, 90, Easing.CubicOut);
        await ConfirmPopupCard.ScaleTo(1.0, 80, Easing.CubicOut);

        bool result = await _confirmPopupCompletionSource.Task;

        await ConfirmPopupOverlay.FadeTo(0, 100, Easing.CubicIn);
        ConfirmPopupOverlay.IsVisible = false;
        ConfirmPopupOverlay.Opacity = 1;
        _confirmPopupCompletionSource = null;

        return result;
    }

    private async void ConfirmPopupPrimaryClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            _confirmPopupCompletionSource?.TrySetResult(true);
        }, "확인 선택");
    }

    private async void ConfirmPopupSecondaryClicked(object sender, EventArgs e)
    {
        await AppExceptionHandler.RunAsync(() =>
        {
            _confirmPopupCompletionSource?.TrySetResult(false);
        }, "취소 선택");
    }

    private static Dictionary<string, PersonalityTest> BuildTests()
    {
        var tests = new Dictionary<string, PersonalityTest>();

        tests["mbti"] = new PersonalityTest("mbti", "🧠", "MBTI 성격유형", "MBTI", BuildMbtiQuestions(), BuildMbtiResults());

        tests["animal"] = new PersonalityTest(
            "animal", "🐾", "동물 성격유형", "동물", BuildAnimalQuestions(),
            new Dictionary<string, TestResult>
            {
                ["cat"] = R("고양이형", "고양이", "🐱", "독립적이고 섬세하며 혼자만의 시간을 중요하게 생각합니다.", "자유를 존중받을 때 가장 편안합니다."),
                ["dog"] = R("강아지형", "강아지", "🐶", "다정하고 친근하며 사람들과 함께할 때 에너지가 살아납니다.", "좋은 관계가 가장 큰 힘이 됩니다."),
                ["lion"] = R("사자형", "사자", "🦁", "추진력과 존재감이 강한 리더형 성향입니다.", "속도보다 주변과의 균형을 함께 보면 더 좋습니다."),
                ["rabbit"] = R("토끼형", "토끼", "🐰", "배려 깊고 조심스러우며 편안한 관계를 중요하게 생각합니다.", "자기 의견도 부드럽게 표현하면 더 좋습니다."),
                ["owl"] = R("부엉이형", "부엉이", "🦉", "관찰력과 분석력이 뛰어난 지혜형 성향입니다.", "생각을 행동으로 옮기는 연습이 도움이 됩니다."),
                ["fox"] = R("여우형", "여우", "🦊", "눈치와 적응력이 좋은 전략형 성향입니다.", "상황 판단력은 강점이지만 진심 표현도 중요합니다."),
                ["dolphin"] = R("돌고래형", "돌고래", "🐬", "밝고 유연하며 사람들과 자연스럽게 소통하는 유형입니다.", "즉흥성과 계획의 균형을 잡으면 더 안정적입니다."),
                ["turtle"] = R("거북이형", "거북이", "🐢", "꾸준하고 성실하며 안정적인 신뢰를 주는 유형입니다.", "변화도 작은 단계로 시도하면 성장의 기회가 됩니다.")
            });

        tests["love"] = new PersonalityTest("love", "💝", "연애 성향 테스트", "연애", BuildLoveQuestions(),
            new Dictionary<string, TestResult>
            {
                ["active"] = R("불꽃 추진형", "추진", "🔥", "좋아하는 마음을 숨기지 않고 빠르게 움직이는 스타일입니다.", "속도만큼 상대의 리듬도 함께 살피면 좋습니다."),
                ["careful"] = R("신중 관찰형", "신중", "🌙", "천천히 마음을 열고 깊은 관계를 선호합니다.", "표현이 부족해 보일 수 있으니 작은 신호를 자주 주세요."),
                ["stable"] = R("다정한 안정형", "안정", "🌿", "신뢰와 꾸준함을 중요하게 생각하는 편안한 연애 스타일입니다.", "지나친 책임감보다 즐거움도 함께 챙기면 좋습니다."),
                ["romantic"] = R("감성 몰입형", "감성", "💗", "표현과 설렘을 중요하게 생각하는 감성형입니다.", "감정의 크기만큼 현실적인 대화도 필요합니다."),
                ["free"] = R("자유 독립형", "자유", "🕊️", "연애 중에도 개인 시간과 취향을 중요하게 생각합니다.", "자유와 무관심이 혼동되지 않도록 표현을 더해보세요.")
            });

        tests["work"] = new PersonalityTest("work", "💼", "직장인 업무유형", "직장", BuildWorkQuestions(),
            new Dictionary<string, TestResult>
            {
                ["leader"] = R("리더형", "리더", "🧭", "방향을 잡고 사람과 일을 끌고 가는 타입입니다.", "권한보다 설득을 함께 쓰면 더 강해집니다."),
                ["analyst"] = R("분석형", "분석", "📊", "자료와 근거를 중시하는 꼼꼼한 타입입니다.", "완벽한 분석보다 실행 시점도 중요합니다."),
                ["executor"] = R("실행형", "실행", "⚙️", "빠르게 움직이고 결과를 만드는 타입입니다.", "속도와 함께 기록을 남기면 신뢰가 높아집니다."),
                ["mediator"] = R("조율형", "조율", "🤝", "사람 사이의 균형과 분위기를 잘 맞추는 타입입니다.", "갈등을 피하기보다 필요한 기준은 분명히 하세요."),
                ["idea"] = R("아이디어형", "기획", "💡", "새로운 방식과 가능성을 잘 찾아내는 타입입니다.", "아이디어를 작은 실행 단위로 쪼개면 성과가 됩니다.")
            });

        tests["money"] = new PersonalityTest("money", "💳", "소비 성향 테스트", "소비", BuildMoneyQuestions(),
            new Dictionary<string, TestResult>
            {
                ["save"] = R("안정 저축형", "저축", "🏦", "미래 안정과 계획을 중요하게 여기는 소비 성향입니다.", "가끔은 현재의 만족에도 예산을 배정해보세요."),
                ["practical"] = R("실속 구매형", "실속", "🛒", "필요성과 실용성을 기준으로 소비하는 타입입니다.", "좋은 선택을 잘하지만 즐거움도 소비의 가치입니다."),
                ["smart"] = R("비교 분석형", "비교", "🔍", "가격, 후기, 조건을 비교해 합리적으로 구매합니다.", "비교가 길어져 피로해지지 않도록 기준을 정하세요."),
                ["enjoy"] = R("기분 충전형", "충전", "🎁", "소비를 통해 기분과 경험을 충전하는 타입입니다.", "소비 후 만족도와 예산 균형을 함께 보면 좋습니다.")
            });

        tests["travel"] = new PersonalityTest("travel", "✈️", "여행 스타일 테스트", "여행", BuildTravelQuestions(),
            new Dictionary<string, TestResult>
            {
                ["planner"] = R("계획 여행형", "계획", "🗺️", "동선과 시간을 잘 짜서 안정적으로 여행하는 타입입니다.", "계획 사이에 여유 시간을 넣으면 만족도가 높아집니다."),
                ["free"] = R("자유 산책형", "자유", "🚶", "즉흥성과 현장 분위기를 즐기는 여행 스타일입니다.", "최소한의 예약과 안전 정보는 챙기면 좋습니다."),
                ["foodie"] = R("맛집 탐험형", "맛집", "🍜", "여행의 핵심을 음식과 지역의 맛에서 찾는 타입입니다.", "동선과 대기 시간을 함께 고려하면 더 편합니다."),
                ["healing"] = R("힐링 휴식형", "힐링", "🌊", "쉼과 분위기를 가장 중요하게 생각하는 여행 스타일입니다.", "무리한 일정은 줄이고 회복감을 우선하세요."),
                ["explorer"] = R("모험 탐색형", "모험", "🧳", "새로운 장소와 경험을 적극적으로 찾는 타입입니다.", "안전과 비용 체크만 해두면 더 자유롭게 즐길 수 있습니다.")
            });

        tests["food"] = new PersonalityTest("food", "🍽️", "음식 취향 성격", "음식", BuildFoodQuestions(),
            new Dictionary<string, TestResult>
            {
                ["comfort"] = R("편안한 집밥형", "편안", "🍚", "익숙하고 안정적인 맛에서 편안함을 느끼는 타입입니다.", "가끔 작은 변화를 더하면 취향의 폭이 넓어집니다."),
                ["adventure"] = R("새맛 도전형", "도전", "🌮", "새로운 음식과 독특한 조합을 즐기는 타입입니다.", "도전 전 기본 정보만 확인하면 실패 확률이 줄어듭니다."),
                ["trend"] = R("인기 맛집형", "맛집", "📸", "후기와 분위기, 트렌드를 잘 살피는 타입입니다.", "유명세보다 내 입맛 기준도 함께 챙기세요."),
                ["passion"] = R("강한 맛 열정형", "열정", "🌶️", "자극적이고 확실한 맛을 좋아하는 타입입니다.", "건강과 컨디션에 맞춰 강도를 조절하면 좋습니다.")
            });

        tests["color"] = new PersonalityTest("color", "🎨", "색깔 성격유형", "색깔", BuildColorQuestions(),
            new Dictionary<string, TestResult>
            {
                ["red"] = R("빨강 에너지형", "빨강", "🔴", "열정과 추진력이 강한 행동형 성향입니다.", "강한 에너지에 쉼을 더하면 지속력이 높아집니다."),
                ["blue"] = R("파랑 신뢰형", "파랑", "🔵", "차분하고 책임감 있는 안정형 성향입니다.", "너무 참기보다 필요한 감정 표현도 중요합니다."),
                ["yellow"] = R("노랑 긍정형", "노랑", "🟡", "밝고 호기심이 많은 분위기 메이커입니다.", "즐거움 속에서도 집중할 우선순위를 정해보세요."),
                ["green"] = R("초록 균형형", "초록", "🟢", "배려와 조화를 중요하게 생각하는 평화형입니다.", "양보만 하지 말고 나의 기준도 말해보세요."),
                ["purple"] = R("보라 감성형", "보라", "🟣", "감성과 개성이 뚜렷한 창의형 성향입니다.", "상상력을 현실화할 작은 실행 계획이 도움이 됩니다.")
            });

        return tests;
    }

    private static List<TestQuestion> BuildMbtiQuestions() => new()
    {
        // MBTI는 4개 선택지 + 가중치 방식으로 구성했습니다.
        // 강한 성향은 2점, 약한 성향은 1점으로 반영되어 결과 신뢰감을 높입니다.
        Q("새로운 모임에 들어갔을 때 나는?", O("먼저 인사하고 대화를 시작한다", "E", 2), O("편한 사람이 보이면 자연스럽게 대화한다", "E", 1), O("분위기를 살피며 천천히 적응한다", "I", 1), O("조용히 관찰하다가 필요한 말만 한다", "I", 2)),
        Q("하루 에너지를 회복하는 방식은?", O("사람들과 만나 이야기하면 힘이 난다", "E", 2), O("가벼운 연락이나 짧은 만남이 도움이 된다", "E", 1), O("혼자 쉬는 시간이 어느 정도 필요하다", "I", 1), O("완전히 혼자 있어야 제대로 회복된다", "I", 2)),
        Q("생각을 정리할 때 나는?", O("말하면서 생각이 정리된다", "E", 2), O("누군가와 나누면 더 잘 정리된다", "E", 1), O("먼저 혼자 정리한 뒤 말하고 싶다", "I", 1), O("충분히 생각하기 전에는 말하지 않는다", "I", 2)),
        Q("새로운 사람을 만났을 때 나는?", O("먼저 질문하고 빠르게 친해진다", "E", 2), O("공통 관심사가 있으면 금방 대화한다", "E", 1), O("상대가 편해질 때까지 거리를 둔다", "I", 1), O("천천히 신뢰가 쌓여야 마음을 연다", "I", 2)),
        Q("주말 약속이 없는 날 나는?", O("누군가를 불러내고 싶어진다", "E", 2), O("가벼운 외출 정도는 좋다", "E", 1), O("집에서 조용히 쉬는 것도 좋다", "I", 1), O("혼자만의 시간을 확보하고 싶다", "I", 2)),
        Q("단체 대화방에서 나는?", O("대화를 자주 이끌고 반응한다", "E", 2), O("재미있는 주제에는 적극 반응한다", "E", 1), O("필요할 때만 답하는 편이다", "I", 1), O("대부분 읽고 조용히 넘어간다", "I", 2)),
        Q("일이나 취미를 시작할 때 나는?", O("같이할 사람을 찾으면 더 즐겁다", "E", 2), O("함께하면 동기부여가 잘 된다", "E", 1), O("혼자 집중할 시간이 있으면 좋다", "I", 1), O("혼자 몰입해야 제대로 할 수 있다", "I", 2)),
        Q("발표나 설명을 맡게 되면?", O("사람 앞에서 말하는 게 크게 부담되지 않는다", "E", 2), O("준비되어 있으면 충분히 할 수 있다", "E", 1), O("가능하면 짧고 차분하게 하고 싶다", "I", 1), O("사람 앞에서 말하는 일은 피하고 싶다", "I", 2)),
        Q("오랜만에 친구에게 연락할 때 나는?", O("생각나면 바로 전화하거나 메시지한다", "E", 2), O("가볍게 안부를 먼저 보낸다", "E", 1), O("타이밍을 조금 고민하고 보낸다", "I", 1), O("연락하고 싶어도 쉽게 먼저 못 한다", "I", 2)),
        Q("사람 많은 행사에 다녀오면?", O("오히려 기분이 살아난다", "E", 2), O("즐거웠지만 조금 쉬면 된다", "E", 1), O("좋았어도 혼자 쉴 시간이 필요하다", "I", 1), O("에너지가 많이 소모되어 오래 쉬고 싶다", "I", 2)),

        Q("새로운 정보를 이해할 때 먼저 보는 것은?", O("구체적인 사실과 실제 사례", "S", 2), O("현재 조건과 적용 가능성", "S", 1), O("전체 흐름과 숨은 의미", "N", 1), O("미래 가능성과 확장 방향", "N", 2)),
        Q("설명을 들을 때 나는?", O("예시와 절차가 있어야 이해가 빠르다", "S", 2), O("실제로 어떻게 쓰는지 알고 싶다", "S", 1), O("원리와 구조를 먼저 알고 싶다", "N", 1), O("큰 그림과 연결성을 먼저 본다", "N", 2)),
        Q("새 아이디어를 들으면?", O("실제로 가능한지 먼저 확인한다", "S", 2), O("필요한 조건을 따져본다", "S", 1), O("응용할 수 있는 방향을 상상한다", "N", 1), O("전혀 다른 가능성까지 떠올린다", "N", 2)),
        Q("문제를 파악하는 방식은?", O("눈에 보이는 자료부터 확인한다", "S", 2), O("현재 발생한 현상을 정리한다", "S", 1), O("반복되는 패턴을 찾는다", "N", 1), O("문제 뒤의 근본 구조를 보려 한다", "N", 2)),
        Q("일을 배울 때 편한 방식은?", O("순서대로 따라 하며 익힌다", "S", 2), O("예제를 보며 감을 잡는다", "S", 1), O("개념을 이해한 뒤 응용한다", "N", 1), O("원리를 알면 스스로 변형해본다", "N", 2)),
        Q("대화 주제로 더 끌리는 것은?", O("실제 경험과 구체적인 이야기", "S", 2), O("오늘 있었던 현실적인 일", "S", 1), O("앞으로의 가능성과 아이디어", "N", 1), O("상상, 비유, 의미가 있는 이야기", "N", 2)),
        Q("계획을 세울 때 나는?", O("실제 일정과 자원을 먼저 본다", "S", 2), O("지금 할 수 있는 범위를 계산한다", "S", 1), O("방향성과 목적을 먼저 본다", "N", 1), O("장기적인 가능성과 변화를 생각한다", "N", 2)),
        Q("자료를 검토할 때 나는?", O("숫자, 날짜, 조건을 꼼꼼히 본다", "S", 2), O("확인 가능한 근거를 중시한다", "S", 1), O("전체 맥락이 맞는지 본다", "N", 1), O("자료가 말하는 흐름과 의미를 본다", "N", 2)),
        Q("익숙한 방식과 새 방식 중 나는?", O("검증된 방식이 가장 안전하다", "S", 2), O("상황에 맞으면 기존 방식을 쓴다", "S", 1), O("더 나은 방법이 있으면 바꿔본다", "N", 1), O("새로운 시도 자체가 흥미롭다", "N", 2)),
        Q("미래를 생각할 때 나는?", O("현재 조건에서 가능한 범위를 본다", "S", 2), O("현실적인 준비를 먼저 떠올린다", "S", 1), O("앞으로 바뀔 흐름을 상상한다", "N", 1), O("아직 없는 기회까지 그려본다", "N", 2)),

        Q("의사결정을 할 때 가장 중요한 것은?", O("논리와 기준이 명확해야 한다", "T", 2), O("객관적 근거가 충분해야 한다", "T", 1), O("사람에게 미칠 영향도 중요하다", "F", 1), O("상대의 감정과 관계를 우선 살핀다", "F", 2)),
        Q("친구가 고민을 말하면 나는?", O("해결책부터 정리해준다", "T", 2), O("문제의 원인을 같이 찾아본다", "T", 1), O("먼저 마음을 이해하려고 한다", "F", 1), O("공감과 위로를 충분히 해준다", "F", 2)),
        Q("의견 충돌이 생기면?", O("맞고 틀린 기준을 분명히 한다", "T", 2), O("합리적인 결론을 찾으려 한다", "T", 1), O("관계가 상하지 않게 조율한다", "F", 1), O("상대가 상처받지 않는 방식을 우선한다", "F", 2)),
        Q("비판을 받을 때 나는?", O("내용이 맞는지 먼저 판단한다", "T", 2), O("개선할 부분을 찾아본다", "T", 1), O("말투와 분위기도 신경 쓰인다", "F", 1), O("감정적으로 꽤 영향을 받는다", "F", 2)),
        Q("팀 규칙을 정할 때 나는?", O("공정하고 일관된 기준이 중요하다", "T", 2), O("예외가 적어야 운영이 쉽다", "T", 1), O("각자의 상황도 고려해야 한다", "F", 1), O("사람들이 받아들일 수 있어야 한다", "F", 2)),
        Q("칭찬을 할 때 나는?", O("성과와 결과를 구체적으로 말한다", "T", 2), O("잘한 부분을 정확히 짚어준다", "T", 1), O("노력과 마음을 함께 인정한다", "F", 1), O("따뜻한 말로 기분을 살려준다", "F", 2)),
        Q("상대가 실수했을 때 나는?", O("원인과 재발 방지를 먼저 본다", "T", 2), O("어떻게 고칠지 정리한다", "T", 1), O("상대가 위축되지 않게 말한다", "F", 1), O("먼저 괜찮은지 살피고 안심시킨다", "F", 2)),
        Q("어려운 선택 앞에서 나는?", O("장단점과 손익을 냉정하게 비교한다", "T", 2), O("근거가 많은 쪽을 택한다", "T", 1), O("내 마음과 주변 사람을 함께 본다", "F", 1), O("관계와 감정의 균형을 가장 중시한다", "F", 2)),
        Q("내가 듣고 싶은 조언은?", O("정확하고 현실적인 조언", "T", 2), O("문제를 해결할 구체적 방법", "T", 1), O("내 마음을 이해해주는 말", "F", 1), O("따뜻한 응원과 공감", "F", 2)),
        Q("갈등을 마무리할 때 나는?", O("결론과 책임을 명확히 해야 한다", "T", 2), O("다음 기준을 정해야 편하다", "T", 1), O("서로 감정이 풀렸는지 확인한다", "F", 1), O("관계가 회복되는 것이 가장 중요하다", "F", 2)),

        Q("일정이 생기면 나는?", O("미리 계획하고 준비해야 편하다", "J", 2), O("대략적인 순서는 정해둔다", "J", 1), O("상황에 따라 바꿀 여지가 필요하다", "P", 1), O("그때그때 유연하게 움직이는 게 좋다", "P", 2)),
        Q("여행 준비를 할 때 나는?", O("숙소와 동선을 미리 확정한다", "J", 2), O("중요한 예약만 먼저 해둔다", "J", 1), O("큰 방향만 정하고 현장에서 고른다", "P", 1), O("즉흥적으로 움직일수록 재미있다", "P", 2)),
        Q("마감이 가까워지면 나는?", O("미리 끝내고 확인하는 편이다", "J", 2), O("중간 점검을 하며 진행한다", "J", 1), O("막판 집중력이 올라오는 편이다", "P", 1), O("압박이 있어야 속도가 붙는다", "P", 2)),
        Q("하루 일과는?", O("계획표가 있어야 마음이 편하다", "J", 2), O("해야 할 일 목록 정도는 필요하다", "J", 1), O("흐름에 맞춰 바꾸는 편이 좋다", "P", 1), O("정해진 틀 없이 자유로운 하루가 좋다", "P", 2)),
        Q("선택지가 많을 때 나는?", O("빨리 정해서 안정감을 얻는다", "J", 2), O("기준을 세워 범위를 줄인다", "J", 1), O("조금 더 가능성을 열어두고 본다", "P", 1), O("마지막까지 선택지를 열어두고 싶다", "P", 2)),
        Q("정리정돈에 가까운 나는?", O("정해진 자리에 있어야 편하다", "J", 2), O("중요한 것만 정리되어 있으면 된다", "J", 1), O("필요할 때 찾을 수 있으면 된다", "P", 1), O("조금 어질러져도 크게 불편하지 않다", "P", 2)),
        Q("갑작스러운 변경이 생기면?", O("계획이 흔들려 불편하다", "J", 2), O("새 계획을 빨리 다시 세운다", "J", 1), O("변경도 받아들일 수 있다", "P", 1), O("오히려 새로운 흐름이 재미있다", "P", 2)),
        Q("프로젝트를 시작할 때 나는?", O("단계와 마감부터 정한다", "J", 2), O("큰 순서를 먼저 잡는다", "J", 1), O("해보면서 방향을 조정한다", "P", 1), O("일단 시작하고 흐름을 만든다", "P", 2)),
        Q("약속을 잡을 때 나는?", O("시간과 장소를 확정해야 편하다", "J", 2), O("주요 조건은 미리 정해두고 싶다", "J", 1), O("대략 정하고 유연하게 움직인다", "P", 1), O("당일 상황에 맞춰 정해도 괜찮다", "P", 2)),
        Q("생활 방식에 가까운 것은?", O("정돈된 루틴이 나를 안정시킨다", "J", 2), O("기본 루틴은 있는 편이 좋다", "J", 1), O("필요할 때 바꿀 수 있어야 편하다", "P", 1), O("자유로운 변화가 있어야 답답하지 않다", "P", 2)),

        Q("회의나 모임 방식은?", O("안건과 결론이 분명해야 좋다", "J", 2), O("시간 배분이 어느 정도 있어야 한다", "J", 1), O("자유롭게 의견을 나누는 게 좋다", "P", 1), O("예상 밖 이야기에서 좋은 아이디어가 나온다", "P", 2)),
        Q("새 취미를 시작할 때 나는?", O("준비물과 방법을 먼저 알아본다", "J", 2), O("기본 정보는 확인하고 시작한다", "J", 1), O("일단 해보며 익히는 게 빠르다", "P", 1), O("즉흥적으로 시도하는 과정이 즐겁다", "P", 2)),
        Q("업무 중간 점검은?", O("자주 확인해야 마음이 놓인다", "J", 2), O("중요한 지점마다 확인한다", "J", 1), O("필요할 때 몰아서 확인해도 된다", "P", 1), O("결과가 나오기 전까지 유동적으로 본다", "P", 2)),
        Q("선택 후 마음가짐은?", O("정한 것을 지키려 한다", "J", 2), O("특별한 이유 없으면 유지한다", "J", 1), O("더 나은 선택이 보이면 바꿀 수 있다", "P", 1), O("결정 후에도 가능성을 계속 본다", "P", 2)),
        Q("갑자기 시간이 비면 나는?", O("밀린 일을 정리한다", "J", 2), O("해야 할 일을 먼저 확인한다", "J", 1), O("기분 가는 대로 시간을 쓴다", "P", 1), O("즉흥적인 활동을 찾아본다", "P", 2)),
        Q("앱이나 도구를 처음 쓸 때?", O("설정과 기능을 먼저 정리한다", "J", 2), O("기본 사용법은 확인한다", "J", 1), O("눌러보며 익숙해진다", "P", 1), O("만지다 보면 자연스럽게 알게 된다", "P", 2)),
        Q("나에게 안정감이란?", O("예측 가능한 계획", "J", 2), O("어느 정도 정해진 기준", "J", 1), O("선택할 수 있는 여유", "P", 1), O("상황에 맞게 바꿀 자유", "P", 2)),
        Q("일을 마친 뒤 나는?", O("체크리스트로 완료를 확인한다", "J", 2), O("빠진 부분이 없는지 본다", "J", 1), O("필요하면 나중에 보완한다", "P", 1), O("일단 끝냈다면 다음 흐름으로 간다", "P", 2)),
        Q("평소 생활에서 더 불편한 것은?", O("정해진 것이 계속 바뀌는 상황", "J", 2), O("계획 없이 우왕좌왕하는 상황", "J", 1), O("너무 빡빡하게 통제되는 상황", "P", 1), O("선택지가 막혀 있는 상황", "P", 2)),
        Q("마지막으로 나와 가까운 문장은?", O("정리되면 마음이 편하다", "J", 2), O("기본 계획은 필요하다", "J", 1), O("여유가 있어야 잘 움직인다", "P", 1), O("자유롭게 바꿀 수 있어야 나답다", "P", 2)),
    };

    private static List<TestQuestion> BuildAnimalQuestions() => new()
    {
        Q("낯선 모임에 들어갔을 때 나는?", O("먼저 웃으며 인사를 건넨다", "dog"), O("한동안 분위기와 사람을 살핀다", "cat"), O("자리 구조와 대화 흐름을 빠르게 파악한다", "fox"), O("편한 사람 옆에서 천천히 적응한다", "rabbit")),
        Q("팀에서 갑자기 결정해야 할 일이 생기면?", O("내가 앞장서 방향을 정한다", "lion"), O("근거를 모아 판단 기준을 만든다", "owl"), O("모두가 불안하지 않게 조율한다", "rabbit"), O("상황에 맞춰 유연한 대안을 찾는다", "dolphin")),
        Q("쉬는 시간이 생겼을 때 가장 끌리는 것은?", O("친한 사람에게 연락해 수다를 나눈다", "dog"), O("혼자 조용히 좋아하는 것을 한다", "cat"), O("관심 주제를 깊게 찾아본다", "owl"), O("밀린 일을 차근차근 정리한다", "turtle")),
        Q("문제가 길어질 때 나는?", O("끝까지 버티며 하나씩 처리한다", "turtle"), O("정면으로 부딪혀 빠르게 끝낸다", "lion"), O("다른 길이 있는지 영리하게 본다", "fox"), O("사람들과 이야기하며 실마리를 찾는다", "dolphin")),
        Q("내가 사람들에게 주는 첫인상은?", O("친근하고 편안하다", "dog"), O("조용하지만 개성이 있다", "cat"), O("카리스마 있고 든든하다", "lion"), O("차분하고 신중하다", "owl")),
        Q("계획이 틀어졌을 때 나는?", O("새 흐름에 맞춰 바로 적응한다", "fox"), O("재미있는 변수로 받아들인다", "dolphin"), O("불편하지만 안전한 쪽부터 확인한다", "rabbit"), O("새 기준을 세워 다시 진행한다", "turtle")),
        Q("칭찬을 받았을 때 내 반응은?", O("기분이 좋아 바로 표현한다", "dog"), O("겉으로는 담담하지만 오래 기억한다", "cat"), O("더 높은 목표를 떠올린다", "lion"), O("쑥스럽지만 고맙게 받아들인다", "rabbit")),
        Q("새로운 취미를 고른다면?", O("사람들과 함께 배우는 활동", "dog"), O("혼자 몰입할 수 있는 취미", "cat"), O("실력이 쌓이는 도전적인 활동", "lion"), O("지식과 관찰이 필요한 취미", "owl")),
        Q("상대의 기분이 달라졌다고 느끼면?", O("바로 눈치채고 말을 조심한다", "fox"), O("걱정되어 조용히 살핀다", "rabbit"), O("직접 물어보고 풀려고 한다", "dog"), O("왜 그런지 상황을 분석한다", "owl")),
        Q("나에게 가장 중요한 관계 방식은?", O("자주 표현하고 함께하는 관계", "dog"), O("서로 자유를 존중하는 관계", "cat"), O("함께 성장하고 목표를 이루는 관계", "lion"), O("오래 믿음을 쌓는 관계", "turtle")),
        Q("위기 상황에서 가까운 행동은?", O("앞장서서 해결을 시작한다", "lion"), O("정보를 정리하고 원인을 찾는다", "owl"), O("주변 사람을 안심시킨다", "rabbit"), O("분위기를 바꿔 긴장을 낮춘다", "dolphin")),
        Q("일을 맡았을 때 나는?", O("책임지고 끝까지 완수한다", "turtle"), O("빠르게 성과를 만든다", "lion"), O("효율적인 순서를 설계한다", "owl"), O("사람들과 협력해 풀어간다", "dog")),
        Q("나의 대화 스타일은?", O("표현이 많고 친근하다", "dog"), O("짧지만 필요한 말만 한다", "cat"), O("상황에 맞게 센스 있게 말한다", "fox"), O("재미있고 밝게 이어간다", "dolphin")),
        Q("낯선 장소에 가면 먼저 하는 일은?", O("주변 구조와 안전한 동선을 본다", "owl"), O("재미있어 보이는 곳을 찾는다", "dolphin"), O("사람들의 분위기를 살핀다", "fox"), O("익숙해질 때까지 천천히 움직인다", "turtle")),
        Q("내가 싫어하는 상황은?", O("지나친 간섭과 통제", "cat"), O("비효율적인 논쟁", "owl"), O("우유부단한 분위기", "lion"), O("갑작스러운 압박", "rabbit")),
        Q("친구가 고민을 말하면?", O("곁에서 따뜻하게 들어준다", "dog"), O("상대가 부담 없게 조심히 돕는다", "rabbit"), O("해결책을 같이 정리한다", "owl"), O("기분이 풀리도록 분위기를 바꾼다", "dolphin")),
        Q("중요한 선택을 앞두면?", O("직감과 추진력으로 결정한다", "lion"), O("장단점과 근거를 비교한다", "owl"), O("상황 흐름과 사람 반응을 본다", "fox"), O("안전하고 익숙한 선택을 선호한다", "turtle")),
        Q("사람 많은 곳에서 나는?", O("활기가 생기고 즐겁다", "dolphin"), O("조금 피곤해 혼자 쉬고 싶다", "cat"), O("전체 분위기를 관찰한다", "fox"), O("편한 사람을 찾는다", "rabbit")),
        Q("실패한 뒤 내 모습은?", O("다시 전략을 짠다", "fox"), O("원인을 기록하고 개선한다", "owl"), O("천천히 회복하며 다시 시작한다", "turtle"), O("주변 위로를 받으면 힘이 난다", "dog")),
        Q("내 생활 리듬에 가까운 것은?", O("자유롭고 개인 시간이 중요하다", "cat"), O("꾸준하고 안정적인 편이다", "turtle"), O("활동적이고 빠른 편이다", "lion"), O("부드럽고 조심스럽게 움직인다", "rabbit")),
        Q("모임에서 내가 맡는 역할은?", O("분위기를 띄우는 역할", "dolphin"), O("사람들을 챙기는 역할", "dog"), O("방향을 잡는 역할", "lion"), O("조용히 필요한 것을 돕는 역할", "rabbit")),
        Q("새 제안을 받으면?", O("흥미로우면 바로 해본다", "dolphin"), O("내 기준에 맞는지 먼저 본다", "cat"), O("성과가 날지 따져본다", "lion"), O("위험과 조건을 확인한다", "owl")),
        Q("나의 강점으로 가장 가까운 것은?", O("친화력", "dog"), O("독립성", "cat"), O("추진력", "lion"), O("성실함", "turtle")),
        Q("혼자 여행한다면?", O("발길 닿는 대로 자유롭게 걷는다", "cat"), O("현지 사람과 대화해본다", "dolphin"), O("계획대로 안정적으로 이동한다", "turtle"), O("목표지를 정하고 강하게 움직인다", "lion")),
        Q("업무나 공부가 많아지면?", O("우선순위와 구조를 정리한다", "owl"), O("바로 처리하기 시작한다", "lion"), O("하나씩 꾸준히 끝낸다", "turtle"), O("도움 받을 사람을 찾는다", "dog")),
        Q("상대가 부탁을 하면?", O("가능하면 먼저 도와준다", "rabbit"), O("내 기준과 여유를 보고 결정한다", "cat"), O("해결 가능성을 계산한다", "owl"), O("필요하면 바로 움직인다", "lion")),
        Q("내가 편안함을 느끼는 환경은?", O("밝고 사람이 있는 곳", "dog"), O("조용하고 간섭 없는 곳", "cat"), O("목표와 역할이 분명한 곳", "lion"), O("규칙과 안정감이 있는 곳", "turtle")),
        Q("논쟁이 생겼을 때 나는?", O("핵심 쟁점을 정리한다", "owl"), O("결론을 내리려 한다", "lion"), O("관계가 상하지 않게 중재한다", "rabbit"), O("상황에 맞는 타협점을 찾는다", "fox")),
        Q("하루를 마무리할 때 나는?", O("내일 할 일을 정리한다", "turtle"), O("오늘 배운 점을 생각한다", "owl"), O("친한 사람과 이야기한다", "dog"), O("혼자 조용히 충전한다", "cat")),
        Q("좋아하는 칭찬은?", O("너랑 있으면 편해", "dog"), O("너답고 멋져", "cat"), O("믿고 맡길 수 있어", "turtle"), O("판단이 정확해", "owl")),
        Q("새로운 환경에서 내 적응 방식은?", O("먼저 분위기를 밝힌다", "dolphin"), O("눈치껏 흐름을 파악한다", "fox"), O("익숙한 루틴을 만든다", "turtle"), O("필요한 정보를 조사한다", "owl")),
        Q("화가 났을 때 나는?", O("혼자 거리를 두고 진정한다", "cat"), O("바로 표현하고 정리하려 한다", "lion"), O("상대가 상처받지 않게 조절한다", "rabbit"), O("왜 화가 났는지 분석한다", "owl")),
        Q("친구 생일을 챙긴다면?", O("정성껏 준비하고 표현한다", "dog"), O("부담 없지만 세심하게 챙긴다", "rabbit"), O("센스 있는 선물을 고른다", "fox"), O("실용적인 것을 준비한다", "turtle")),
        Q("결과보다 과정이 흔들릴 때?", O("방향을 다시 잡고 밀어붙인다", "lion"), O("원인을 찾아 체계를 고친다", "owl"), O("사람들 마음을 먼저 살핀다", "rabbit"), O("분위기를 바꿔 다시 흐르게 한다", "dolphin")),
        Q("내가 좋아하는 속도는?", O("빠르고 분명한 속도", "lion"), O("차분하고 안정적인 속도", "turtle"), O("자유롭게 바뀌는 속도", "cat"), O("상황에 맞는 유연한 속도", "fox")),
        Q("누군가 나를 평가하면?", O("내용이 맞는지 따져본다", "owl"), O("감정적으로 조금 신경 쓰인다", "rabbit"), O("더 잘할 동기로 삼는다", "lion"), O("나답게 받아들이려 한다", "cat")),
        Q("모르는 분야를 접하면?", O("원리와 배경부터 파악한다", "owl"), O("일단 해보며 배운다", "dolphin"), O("성과가 날 방법을 찾는다", "lion"), O("천천히 반복해 익힌다", "turtle")),
        Q("내가 가장 오래 유지하는 힘은?", O("사람들과의 정", "dog"), O("나만의 기준", "cat"), O("목표 의식", "lion"), O("꾸준한 습관", "turtle")),
        Q("팀원이 실수했을 때?", O("다음 해결책을 빠르게 제시한다", "lion"), O("원인을 함께 분석한다", "owl"), O("먼저 괜찮은지 살핀다", "rabbit"), O("다시 분위기를 좋게 만든다", "dolphin")),
        Q("내가 끌리는 사람은?", O("따뜻하게 반응하는 사람", "dog"), O("서로 독립적인 사람", "cat"), O("배울 점이 있는 사람", "owl"), O("믿음직하고 꾸준한 사람", "turtle")),
        Q("스트레스가 쌓이면?", O("혼자 쉬어야 풀린다", "cat"), O("누군가와 이야기해야 풀린다", "dog"), O("해야 할 일을 정리하면 풀린다", "turtle"), O("원인을 제거해야 풀린다", "lion")),
        Q("내가 가진 매력은?", O("따뜻함과 친근함", "dog"), O("차분한 신비로움", "cat"), O("강한 존재감", "lion"), O("지혜로운 관찰력", "owl")),
        Q("새로운 사람과 친해지는 방식은?", O("먼저 말을 걸고 가까워진다", "dog"), O("공통점을 찾으며 자연스럽게 간다", "dolphin"), O("상대의 태도를 보고 거리를 조절한다", "fox"), O("시간을 두고 신뢰를 쌓는다", "turtle")),
        Q("계속 반복되는 일에는?", O("꾸준히 잘 해낸다", "turtle"), O("비효율을 찾아 개선한다", "owl"), O("빨리 끝낼 방법을 찾는다", "lion"), O("지루하면 변화를 주고 싶다", "dolphin")),
        Q("갑작스러운 기회가 오면?", O("붙잡고 도전한다", "lion"), O("조건을 따져본다", "owl"), O("흐름을 보고 유연하게 움직인다", "fox"), O("내 생활이 흔들릴지 본다", "turtle")),
        Q("내가 듣기 싫은 말은?", O("너무 차가워 보여", "cat"), O("너무 느려", "turtle"), O("너무 세게 말해", "lion"), O("너무 눈치 봐", "rabbit")),
        Q("좋아하는 공간은?", O("대화가 많은 활기찬 공간", "dog"), O("조용하고 감각적인 공간", "cat"), O("생각하기 좋은 정돈된 공간", "owl"), O("편안하고 안정적인 공간", "rabbit")),
        Q("내가 성장하려면 필요한 것은?", O("나의 감정도 표현하기", "cat"), O("타인의 속도 존중하기", "lion"), O("고민보다 실행 늘리기", "owl"), O("새 변화에 조금 더 열리기", "turtle")),
        Q("마지막으로 나와 가장 가까운 문장은?", O("사람과 함께할 때 힘이 난다", "dog"), O("나만의 시간이 꼭 필요하다", "cat"), O("목표가 있으면 강해진다", "lion"), O("천천히 가도 끝까지 간다", "turtle")),
        Q("상대가 나를 오해했을 때?", O("바로 설명하고 풀려고 한다", "dog"), O("잠시 거리를 두고 생각한다", "cat"), O("핵심을 정리해 차분히 말한다", "owl"), O("상대 마음이 상하지 않게 조심한다", "rabbit")),
    };

    private static List<TestQuestion> BuildLoveQuestions() => new()
    {
        Q("마음에 드는 사람이 생기면?", O("먼저 연락하고 표현한다", "active"), O("상대 반응을 천천히 본다", "careful"), O("편안한 관계부터 쌓는다", "stable"), O("설렘을 숨기기 어렵다", "romantic"), O("내 생활 리듬도 지키고 싶다", "free")),
        Q("연애에서 가장 중요한 것은?", O("서로 믿고 꾸준히 만나는 것", "stable"), O("두근거림과 감정 표현", "romantic"), O("서로의 자유와 공간", "free"), O("확실한 관심 표현", "active"), O("천천히 깊어지는 신뢰", "careful")),
        Q("첫 데이트 장소는?", O("분위기 좋은 곳을 적극 제안한다", "active"), O("조용히 대화하기 좋은 곳", "careful"), O("부담 없는 익숙한 곳", "stable"), O("특별한 감성이 있는 곳", "romantic"), O("각자 취향을 존중할 수 있는 곳", "free")),
        Q("연락 빈도는?", O("자주 연락해야 마음이 놓인다", "romantic"), O("필요할 때 꾸준히 하면 된다", "stable"), O("각자 시간도 충분해야 한다", "free"), O("내가 먼저 자주 하는 편이다", "active"), O("상대 속도를 보며 맞춘다", "careful")),
        Q("갈등이 생기면?", O("바로 이야기하고 풀고 싶다", "active"), O("감정이 가라앉은 뒤 말한다", "careful"), O("관계를 지키는 방향으로 조율한다", "stable"), O("서운함을 감정적으로 표현한다", "romantic"), O("잠시 거리를 두고 생각한다", "free")),
        Q("사랑을 표현하는 방식은?", O("말과 행동으로 적극 표현한다", "active"), O("작은 배려를 꾸준히 한다", "stable"), O("편지나 분위기 있는 표현을 좋아한다", "romantic"), O("상대가 필요할 때 조용히 챙긴다", "careful"), O("간섭하지 않는 것도 사랑이라고 본다", "free")),
        Q("상대가 바쁘다고 연락이 줄면?", O("왜 그런지 바로 확인한다", "active"), O("서운하고 불안해진다", "romantic"), O("상황을 이해하고 기다린다", "stable"), O("조심스레 이유를 살핀다", "careful"), O("나도 내 시간을 보낸다", "free")),
        Q("기념일은?", O("제대로 챙기고 싶다", "romantic"), O("부담 없지만 꾸준히 기억한다", "stable"), O("상대가 원하면 맞춘다", "free"), O("이벤트를 준비하고 싶다", "active"), O("과하지 않게 조용히 챙긴다", "careful")),
        Q("상대의 친구 모임에 초대받으면?", O("즐겁게 참여해 친해진다", "active"), O("조금 긴장하지만 예의 있게 간다", "careful"), O("관계를 위해 자연스럽게 함께한다", "stable"), O("상대와 함께라면 설렌다", "romantic"), O("매번 함께해야 한다면 부담스럽다", "free")),
        Q("연애 초반 내 모습은?", O("빠르게 가까워진다", "active"), O("천천히 확인한다", "careful"), O("편안함을 먼저 만든다", "stable"), O("감정이 크게 움직인다", "romantic"), O("내 생활 균형을 유지한다", "free")),
        Q("상대가 서운함을 말하면?", O("바로 사과하고 행동을 바꾼다", "active"), O("무엇이 문제였는지 조심히 듣는다", "careful"), O("관계가 흔들리지 않게 설명한다", "stable"), O("나도 감정이 올라온다", "romantic"), O("서로의 기준 차이를 이야기한다", "free")),
        Q("선물 고르는 방식은?", O("상대가 좋아할 걸 적극 찾아본다", "active"), O("실용적이고 오래 쓸 것을 고른다", "stable"), O("의미 있는 감성 선물을 고른다", "romantic"), O("부담스럽지 않은 것을 고른다", "careful"), O("상대 취향을 존중하는 선택권을 준다", "free")),
        Q("연애 중 혼자만의 시간은?", O("조금 줄어도 괜찮다", "active"), O("서로 적당히 필요하다", "stable"), O("꼭 보장되어야 한다", "free"), O("상대가 이해해주길 바란다", "careful"), O("혼자 있어도 감정 연결은 필요하다", "romantic")),
        Q("좋아한다는 확신이 들 때는?", O("계속 보고 싶고 표현하고 싶을 때", "active"), O("함께 있으면 안정될 때", "stable"), O("작은 말에도 설렐 때", "romantic"), O("오래 지켜봐도 믿음이 갈 때", "careful"), O("내 자유를 존중해줄 때", "free")),
        Q("상대의 단점을 보면?", O("고쳐가자고 바로 말한다", "active"), O("관계 속에서 자연스럽게 맞춘다", "stable"), O("감정적으로 크게 받아들일 수 있다", "romantic"), O("시간을 두고 판단한다", "careful"), O("서로 다름으로 인정하려 한다", "free")),
        Q("데이트 계획은?", O("내가 주도해 정하는 편이다", "active"), O("상대와 상의해 안정적으로 짠다", "stable"), O("분위기와 감성을 중요하게 본다", "romantic"), O("상대가 불편하지 않은지 확인한다", "careful"), O("그날 기분에 맞게 자유롭게 한다", "free")),
        Q("상대에게 듣고 싶은 말은?", O("보고 싶어, 지금 만나자", "active"), O("늘 곁에 있어줄게", "stable"), O("너는 나에게 특별해", "romantic"), O("천천히 알아가자", "careful"), O("너답게 지내도 괜찮아", "free")),
        Q("권태기가 오면?", O("새로운 시도를 하자고 제안한다", "active"), O("대화를 통해 관계를 정비한다", "stable"), O("감정이 식은 건지 고민한다", "romantic"), O("조심스럽게 거리를 살핀다", "careful"), O("각자 시간을 갖는 것도 방법이다", "free")),
        Q("공개 연애에 대해?", O("좋으면 자연스럽게 알린다", "active"), O("가까운 사람에게만 알린다", "careful"), O("상대와 합의해 정한다", "stable"), O("특별한 순간을 공유하고 싶다", "romantic"), O("사생활은 지키고 싶다", "free")),
        Q("이별을 생각하게 되는 순간은?", O("관심과 표현이 완전히 사라질 때", "romantic"), O("신뢰가 깨졌을 때", "stable"), O("내 자유가 계속 침해될 때", "free"), O("상대가 계속 미루고 피할 때", "active"), O("오래 봐도 확신이 안 생길 때", "careful")),
        Q("상대와 취미가 다르면?", O("같이 해보자고 제안한다", "active"), O("서로 취향을 인정한다", "free"), O("공통 취미를 하나 만든다", "stable"), O("상대 취향을 조심히 알아본다", "careful"), O("함께하는 시간이면 무엇이든 좋다", "romantic")),
        Q("사소한 약속은?", O("잘 지켜야 마음이 놓인다", "stable"), O("상대에게 중요한 일이면 꼭 챙긴다", "careful"), O("가끔 즉흥 변경도 괜찮다", "free"), O("바로바로 정하고 실행한다", "active"), O("약속보다 마음 표현이 더 중요할 때도 있다", "romantic")),
        Q("상대가 힘든 일을 겪으면?", O("바로 달려가 도와준다", "active"), O("묵묵히 곁을 지킨다", "stable"), O("감정적으로 함께 아파한다", "romantic"), O("필요한 도움을 조심히 묻는다", "careful"), O("상대가 원할 때까지 공간을 준다", "free")),
        Q("연애에서 불안한 순간은?", O("상대 표현이 줄어들 때", "romantic"), O("관계 방향이 불분명할 때", "active"), O("신뢰가 흔들릴 때", "stable"), O("상대 마음을 아직 모르겠을 때", "careful"), O("내 시간이 사라질 때", "free")),
        Q("상대 가족이나 지인을 만날 때?", O("적극적으로 좋은 인상을 남긴다", "active"), O("예의 있게 조심히 행동한다", "careful"), O("자연스럽고 안정적으로 어울린다", "stable"), O("상대와 더 가까워진 느낌이 든다", "romantic"), O("너무 빠른 만남은 부담스럽다", "free")),
        Q("장거리 연애라면?", O("자주 연락하고 계획적으로 만난다", "stable"), O("감정 표현을 더 많이 해야 한다", "romantic"), O("각자 생활을 존중하면 가능하다", "free"), O("만날 방법을 적극 찾는다", "active"), O("확신이 생길 때까지 신중하다", "careful")),
        Q("연애 중 돈 문제는?", O("서로 기준을 정해두면 좋다", "stable"), O("상황에 따라 내가 더 낼 수 있다", "active"), O("기념일에는 아끼지 않고 싶다", "romantic"), O("부담이 되지 않게 조심한다", "careful"), O("각자 경제권을 존중해야 한다", "free")),
        Q("데이트가 취소되면?", O("바로 다른 날을 잡는다", "active"), O("아쉽지만 이해한다", "stable"), O("서운한 마음이 오래 간다", "romantic"), O("이유를 조심스럽게 확인한다", "careful"), O("혼자 시간을 보내면 된다", "free")),
        Q("내 연애 속도는?", O("빠르게 가까워지는 편", "active"), O("천천히 깊어지는 편", "careful"), O("일정하고 꾸준한 편", "stable"), O("감정에 따라 진해지는 편", "romantic"), O("자유로운 거리를 유지하는 편", "free")),
        Q("상대가 나를 통제하려 하면?", O("분명하게 싫다고 말한다", "free"), O("대화로 기준을 조정한다", "stable"), O("감정적으로 답답해진다", "romantic"), O("바로 문제를 제기한다", "active"), O("왜 그런지 먼저 살핀다", "careful")),
        Q("프로포즈나 큰 이벤트는?", O("특별하고 감동적이면 좋다", "romantic"), O("진심이 담긴 안정적인 방식이 좋다", "stable"), O("둘만의 방식이면 충분하다", "free"), O("확실하고 멋지게 하고 싶다", "active"), O("과한 주목은 부담스럽다", "careful")),
        Q("상대에게 화가 났을 때?", O("바로 말하고 풀고 싶다", "active"), O("시간을 두고 정리한 뒤 말한다", "careful"), O("관계가 상하지 않게 표현한다", "stable"), O("감정이 얼굴에 드러난다", "romantic"), O("혼자 진정할 공간이 필요하다", "free")),
        Q("연애와 일/취미의 균형은?", O("연애가 우선이 될 때가 많다", "romantic"), O("상황에 맞게 균형을 잡는다", "stable"), O("내 일과 취미도 중요하다", "free"), O("좋아하면 시간을 만들어낸다", "active"), O("상대가 부담 느끼지 않게 맞춘다", "careful")),
        Q("상대가 무심한 편이라면?", O("표현을 더 해달라고 말한다", "active"), O("상처받고 혼자 고민한다", "romantic"), O("그 사람 방식인지 이해해본다", "stable"), O("천천히 관찰한다", "careful"), O("나도 너무 매달리지 않는다", "free")),
        Q("친구가 연애 조언을 하면?", O("바로 참고해 행동한다", "active"), O("내 상황과 비교해본다", "careful"), O("관계 안정에 도움 되면 듣는다", "stable"), O("감정적으로 공감받고 싶다", "romantic"), O("내 선택은 내가 한다", "free")),
        Q("연애 중 가장 큰 장점은?", O("표현력과 추진력", "active"), O("신중함과 배려", "careful"), O("꾸준함과 신뢰", "stable"), O("감성적 몰입", "romantic"), O("존중과 독립성", "free")),
        Q("상대와 미래 이야기를 할 때?", O("구체적으로 빨리 정하고 싶다", "active"), O("충분히 알아본 뒤 이야기하고 싶다", "careful"), O("현실적인 계획을 함께 세운다", "stable"), O("상상만 해도 설렌다", "romantic"), O("미래보다 현재의 자유도 중요하다", "free")),
        Q("다툰 뒤 화해 방식은?", O("먼저 다가가 대화한다", "active"), O("차분히 사과와 기준을 나눈다", "stable"), O("진심 어린 말과 표현이 필요하다", "romantic"), O("어색함이 풀릴 시간이 필요하다", "careful"), O("각자 생각할 시간을 가진다", "free")),
        Q("상대가 내 취향을 기억해주면?", O("크게 감동한다", "romantic"), O("신뢰가 쌓인다", "stable"), O("나도 더 표현하게 된다", "active"), O("조용히 고마움을 느낀다", "careful"), O("기쁘지만 부담은 없어야 한다", "free")),
        Q("연애에서 나를 지치게 하는 것은?", O("계속 애매한 태도", "active"), O("급하게 몰아붙이는 분위기", "careful"), O("약속과 신뢰가 깨지는 것", "stable"), O("감정 표현이 부족한 것", "romantic"), O("간섭과 소유욕", "free")),
        Q("둘만의 규칙을 만든다면?", O("연락과 만남 기준을 정한다", "stable"), O("서운하면 바로 말하기", "active"), O("기념일과 표현 챙기기", "romantic"), O("서로의 개인 시간 보장하기", "free"), O("부담 없는 속도로 만나기", "careful")),
        Q("상대가 새 도전을 한다면?", O("적극 응원하고 도와준다", "active"), O("현실적으로 필요한 것을 챙긴다", "stable"), O("함께 설레고 기대한다", "romantic"), O("무리하지 않는지 조심히 본다", "careful"), O("상대 선택을 존중한다", "free")),
        Q("내가 원하는 안정감은?", O("말과 행동이 꾸준한 사람", "stable"), O("확실히 표현하는 사람", "active"), O("감정을 깊이 나누는 사람", "romantic"), O("천천히 믿음을 주는 사람", "careful"), O("나를 있는 그대로 두는 사람", "free")),
        Q("관계가 깊어질수록 나는?", O("더 적극적으로 챙긴다", "active"), O("더 편안하고 안정된다", "stable"), O("감정적으로 더 몰입한다", "romantic"), O("조심스레 마음을 더 연다", "careful"), O("가까워져도 나만의 공간은 필요하다", "free")),
        Q("연애에서 내가 피하고 싶은 모습은?", O("혼자 앞서가는 것", "active"), O("표현을 너무 아끼는 것", "careful"), O("익숙함에 무심해지는 것", "stable"), O("감정에만 휩쓸리는 것", "romantic"), O("무관심해 보이는 것", "free")),
        Q("상대가 가장 좋아할 내 모습은?", O("솔직하고 적극적인 표현", "active"), O("조심스럽지만 깊은 마음", "careful"), O("믿을 수 있는 꾸준함", "stable"), O("따뜻한 설렘과 감성", "romantic"), O("존중하고 여유로운 태도", "free")),
        Q("마지막으로 내 연애에 가까운 문장은?", O("좋으면 표현하고 움직여야 한다", "active"), O("천천히 알아가야 오래 간다", "careful"), O("사랑은 꾸준한 신뢰다", "stable"), O("사랑은 마음을 깊게 나누는 일이다", "romantic"), O("사랑해도 나다움은 지켜야 한다", "free")),
        Q("상대와 연락 스타일이 다르면?", O("맞춰달라고 솔직히 말한다", "active"), O("천천히 적응할 시간을 둔다", "careful"), O("서로 가능한 기준을 정한다", "stable"), O("서운함을 감정적으로 느낀다", "romantic"), O("각자 방식도 존중해야 한다", "free")),
        Q("오래 만난 관계에서 필요한 것은?", O("새로운 데이트와 변화", "active"), O("서로를 다시 알아가는 시간", "careful"), O("꾸준한 약속과 신뢰", "stable"), O("설렘을 살리는 표현", "romantic"), O("각자의 성장과 여유", "free")),
        Q("내가 사랑받는다고 느끼는 순간은?", O("상대가 먼저 다가올 때", "active"), O("내 속도를 기다려줄 때", "careful"), O("약속을 꾸준히 지킬 때", "stable"), O("진심 어린 말로 표현할 때", "romantic"), O("나의 자유를 인정해줄 때", "free")),
    };

    private static List<TestQuestion> BuildWorkQuestions() => new()
    {
        Q("새 프로젝트를 시작하면?", O("목표와 역할을 먼저 정한다", "leader"), O("자료와 요구사항을 분석한다", "analyst"), O("바로 실행 가능한 일부터 한다", "executor"), O("관련 부서 의견을 모은다", "mediator"), O("새로운 접근 방식을 제안한다", "idea")),
        Q("회의에서 나는?", O("결론과 담당자를 정리한다", "leader"), O("수치와 근거를 확인한다", "analyst"), O("실행 일정부터 묻는다", "executor"), O("의견 충돌을 부드럽게 조율한다", "mediator"), O("다른 가능성을 던져본다", "idea")),
        Q("업무 지시가 애매하면?", O("방향을 재정의해 확인한다", "leader"), O("누락된 정보를 요청한다", "analyst"), O("가능한 범위부터 처리한다", "executor"), O("이해관계자에게 확인한다", "mediator"), O("여러 시나리오를 떠올린다", "idea")),
        Q("마감이 가까워지면?", O("우선순위를 조정해 지휘한다", "leader"), O("리스크와 오류를 점검한다", "analyst"), O("속도를 올려 끝낸다", "executor"), O("도움이 필요한 사람을 연결한다", "mediator"), O("효율을 높일 아이디어를 낸다", "idea")),
        Q("새 시스템을 도입한다면?", O("도입 목적과 책임자를 명확히 한다", "leader"), O("기능과 데이터 흐름을 검토한다", "analyst"), O("테스트 화면부터 만져본다", "executor"), O("사용자 교육과 반응을 살핀다", "mediator"), O("업무 방식을 바꿀 기회를 찾는다", "idea")),
        Q("상사가 갑자기 방향을 바꾸면?", O("팀 방향을 다시 정렬한다", "leader"), O("변경 근거를 확인한다", "analyst"), O("바뀐 기준에 맞춰 바로 수정한다", "executor"), O("팀원 혼란을 줄인다", "mediator"), O("새 방향의 가능성을 본다", "idea")),
        Q("동료가 업무를 늦추면?", O("일정을 다시 배분한다", "leader"), O("병목 원인을 찾는다", "analyst"), O("내가 처리할 수 있는 부분을 한다", "executor"), O("대화를 통해 부담을 조정한다", "mediator"), O("일하는 방식을 바꿔보자고 한다", "idea")),
        Q("보고서를 만들 때?", O("핵심 결론부터 잡는다", "leader"), O("근거 자료와 수치를 정리한다", "analyst"), O("일단 초안을 빠르게 완성한다", "executor"), O("받는 사람이 이해하기 쉽게 쓴다", "mediator"), O("눈에 띄는 구성 방식을 고민한다", "idea")),
        Q("고객 불만이 들어오면?", O("책임지고 처리 방향을 정한다", "leader"), O("원인과 재발 가능성을 분석한다", "analyst"), O("즉시 가능한 조치부터 한다", "executor"), O("고객 감정을 먼저 안정시킨다", "mediator"), O("서비스 개선 아이디어로 연결한다", "idea")),
        Q("성과 평가에서 강점은?", O("성과를 만드는 방향성", "leader"), O("정확한 분석과 검증", "analyst"), O("빠른 처리와 실행력", "executor"), O("협업과 관계 관리", "mediator"), O("개선 제안과 창의성", "idea")),
        Q("새 아이디어를 들으면?", O("목표에 맞는지 판단한다", "leader"), O("실현 조건을 따져본다", "analyst"), O("작게라도 해본다", "executor"), O("사람들이 받아들일지 본다", "mediator"), O("더 확장할 방법을 찾는다", "idea")),
        Q("업무 실수가 나면?", O("책임 소재와 대응을 정리한다", "leader"), O("원인 분석표를 만든다", "analyst"), O("즉시 수정한다", "executor"), O("관련자에게 설명하고 양해를 구한다", "mediator"), O("실수 방지 아이디어를 만든다", "idea")),
        Q("교육을 맡으면?", O("학습 목표와 과정을 설계한다", "leader"), O("자료를 체계적으로 준비한다", "analyst"), O("실습 중심으로 진행한다", "executor"), O("수강자 수준에 맞춘다", "mediator"), O("재미있는 사례를 넣는다", "idea")),
        Q("업무 우선순위는?", O("조직 목표와 영향도", "leader"), O("데이터와 리스크", "analyst"), O("당장 처리 가능 여부", "executor"), O("협업 일정과 사람 영향", "mediator"), O("개선 효과와 새 가능성", "idea")),
        Q("야근이 예상되면?", O("역할을 재배치한다", "leader"), O("왜 늦어졌는지 확인한다", "analyst"), O("필요하면 집중해서 끝낸다", "executor"), O("팀원 피로도를 살핀다", "mediator"), O("일을 줄일 방법을 찾는다", "idea")),
        Q("업무 메신저에서 나는?", O("핵심 지시와 결론을 남긴다", "leader"), O("근거 자료 링크를 공유한다", "analyst"), O("처리 결과를 빠르게 보낸다", "executor"), O("상대가 오해하지 않게 쓴다", "mediator"), O("새 제안을 가볍게 던진다", "idea")),
        Q("품질 문제가 발견되면?", O("중단/진행 판단을 내린다", "leader"), O("검사 기준과 데이터를 확인한다", "analyst"), O("현장 조치를 먼저 한다", "executor"), O("관련 부서 협조를 구한다", "mediator"), O("공정 개선 방향을 생각한다", "idea")),
        Q("내가 선호하는 업무는?", O("사람과 방향을 이끄는 일", "leader"), O("숫자와 구조를 다루는 일", "analyst"), O("결과물을 만드는 일", "executor"), O("부서 간 연결하는 일", "mediator"), O("새 서비스를 기획하는 일", "idea")),
        Q("팀 분위기가 가라앉으면?", O("목표를 다시 상기시킨다", "leader"), O("문제 원인을 조용히 파악한다", "analyst"), O("작은 성과부터 만들자고 한다", "executor"), O("사람들의 이야기를 들어준다", "mediator"), O("새로운 방식으로 분위기를 바꾼다", "idea")),
        Q("회의가 길어질 때?", O("결론을 내고 정리한다", "leader"), O("논점별로 분류한다", "analyst"), O("실행 항목만 뽑는다", "executor"), O("모두의 의견을 정리한다", "mediator"), O("다른 관점의 질문을 던진다", "idea")),
        Q("자료가 부족한 상태라면?", O("현재 기준으로 방향을 정한다", "leader"), O("자료 보완부터 요청한다", "analyst"), O("시범 실행으로 확인한다", "executor"), O("관련자 경험을 들어본다", "mediator"), O("가설을 세워본다", "idea")),
        Q("일 잘한다는 말은 언제 듣나?", O("팀을 잘 이끌 때", "leader"), O("꼼꼼하고 정확할 때", "analyst"), O("빠르게 끝낼 때", "executor"), O("사람들과 잘 맞출 때", "mediator"), O("새로운 답을 낼 때", "idea")),
        Q("싫어하는 업무 상황은?", O("방향 없이 끌려가는 상황", "leader"), O("근거 없이 결정되는 상황", "analyst"), O("말만 많고 실행 없는 상황", "executor"), O("갈등이 방치되는 상황", "mediator"), O("새 시도가 막히는 상황", "idea")),
        Q("업무 자동화를 본다면?", O("성과와 책임 범위를 본다", "leader"), O("데이터 정확도를 본다", "analyst"), O("반복 업무부터 적용한다", "executor"), O("사용자 적응을 본다", "mediator"), O("새 비즈니스 가능성을 본다", "idea")),
        Q("긴급 장애가 나면?", O("상황실처럼 역할을 나눈다", "leader"), O("로그와 원인을 추적한다", "analyst"), O("우선 복구 조치를 한다", "executor"), O("고객과 내부 공유를 관리한다", "mediator"), O("장기 개선 구조를 떠올린다", "idea")),
        Q("협업 툴을 사용할 때?", O("담당자와 마감일을 명확히 한다", "leader"), O("이력과 데이터를 정리한다", "analyst"), O("체크리스트를 빠르게 처리한다", "executor"), O("댓글로 소통을 부드럽게 한다", "mediator"), O("새 템플릿을 만들어본다", "idea")),
        Q("내 커리어 관심사는?", O("관리자와 책임자 역할", "leader"), O("전문가와 분석가 역할", "analyst"), O("실무 고수와 해결사 역할", "executor"), O("조직문화와 협업 역할", "mediator"), O("기획자와 혁신 역할", "idea")),
        Q("성과가 안 나올 때?", O("목표와 방법을 다시 잡는다", "leader"), O("지표를 보고 원인을 찾는다", "analyst"), O("행동량을 늘린다", "executor"), O("팀 내 협업 문제를 살핀다", "mediator"), O("완전히 다른 시도를 한다", "idea")),
        Q("새로운 팀에 배치되면?", O("역할과 기대치를 확인한다", "leader"), O("업무 프로세스를 파악한다", "analyst"), O("작은 일부터 빨리 기여한다", "executor"), O("사람들과 관계를 만든다", "mediator"), O("개선할 부분을 찾는다", "idea")),
        Q("고객 제안서를 쓴다면?", O("핵심 가치와 전략을 강조한다", "leader"), O("비용과 효과 근거를 넣는다", "analyst"), O("구현 일정과 산출물을 제시한다", "executor"), O("고객 입장 언어로 풀어쓴다", "mediator"), O("차별화 아이디어를 넣는다", "idea")),
        Q("반복 업무를 할 때?", O("기준을 잡고 분배한다", "leader"), O("오류 패턴을 찾는다", "analyst"), O("빠르게 처리한다", "executor"), O("함께 하는 사람을 챙긴다", "mediator"), O("자동화 방법을 찾는다", "idea")),
        Q("업무 피드백을 받으면?", O("다음 목표에 반영한다", "leader"), O("타당한 근거인지 검토한다", "analyst"), O("바로 수정한다", "executor"), O("말한 의도를 이해하려 한다", "mediator"), O("새 방향으로 바꿔본다", "idea")),
        Q("내 책상이나 파일은?", O("프로젝트별로 정리한다", "leader"), O("자료 출처와 버전을 챙긴다", "analyst"), O("필요한 것만 빠르게 찾게 둔다", "executor"), O("공유하기 쉽게 정리한다", "mediator"), O("아이디어 메모가 많다", "idea")),
        Q("팀장이 된다면?", O("목표와 책임을 분명히 한다", "leader"), O("성과 지표를 설계한다", "analyst"), O("실행 속도를 높인다", "executor"), O("팀원 간 소통을 챙긴다", "mediator"), O("새로운 시도를 장려한다", "idea")),
        Q("신입에게 알려준다면?", O("전체 방향과 기대치를 설명한다", "leader"), O("업무 원리와 자료 위치를 알려준다", "analyst"), O("바로 따라 할 일을 준다", "executor"), O("적응할 수 있게 자주 확인한다", "mediator"), O("스스로 생각할 질문을 준다", "idea")),
        Q("업무 중 가장 뿌듯한 순간은?", O("팀이 목표를 달성할 때", "leader"), O("분석이 맞아떨어질 때", "analyst"), O("막힌 일을 해결했을 때", "executor"), O("사람들이 편해졌을 때", "mediator"), O("내 아이디어가 채택될 때", "idea")),
        Q("불필요한 절차를 보면?", O("바꿀 권한과 방향을 찾는다", "leader"), O("근거 자료를 모은다", "analyst"), O("바로 줄일 수 있는 부분부터 줄인다", "executor"), O("관련자 부담을 확인한다", "mediator"), O("새 프로세스를 상상한다", "idea")),
        Q("보고받는 입장이라면?", O("결론과 요청 사항을 먼저 원한다", "leader"), O("근거와 데이터가 필요하다", "analyst"), O("진행 상황과 완료일이 궁금하다", "executor"), O("이슈와 협업 상황도 듣고 싶다", "mediator"), O("대안과 제안도 듣고 싶다", "idea")),
        Q("업무에서 신뢰를 주는 방식은?", O("책임 있게 결정한다", "leader"), O("정확하게 검토한다", "analyst"), O("약속한 일을 끝낸다", "executor"), O("사람을 배려하며 소통한다", "mediator"), O("새로운 가치를 만든다", "idea")),
        Q("혼자 일할 때 나는?", O("목표를 세워 스스로 끌고 간다", "leader"), O("깊게 파고들어 정리한다", "analyst"), O("몰입해서 빠르게 처리한다", "executor"), O("중간 공유가 필요하다", "mediator"), O("여러 아이디어를 실험한다", "idea")),
        Q("여러 부서가 얽힌 일은?", O("의사결정 구조를 만든다", "leader"), O("데이터 기준을 통일한다", "analyst"), O("실행 담당을 나눠 처리한다", "executor"), O("부서 간 입장을 조율한다", "mediator"), O("새 협업 모델을 제안한다", "idea")),
        Q("내가 놓치기 쉬운 것은?", O("너무 앞서가 사람 마음을 놓침", "leader"), O("분석이 길어 실행이 늦음", "analyst"), O("속도 때문에 기록이 부족함", "executor"), O("조율하다 기준이 흐려짐", "mediator"), O("아이디어가 많아 마무리가 약함", "idea")),
        Q("성과 발표를 한다면?", O("큰 방향과 결과를 강하게 말한다", "leader"), O("수치와 근거를 중심으로 말한다", "analyst"), O("실행 과정과 완료 내용을 말한다", "executor"), O("협업과 기여를 함께 말한다", "mediator"), O("새로운 의미와 확장성을 말한다", "idea")),
        Q("새 업무를 배울 때?", O("전체 목적부터 이해한다", "leader"), O("매뉴얼과 데이터를 읽는다", "analyst"), O("해보면서 익힌다", "executor"), O("잘 아는 사람에게 묻는다", "mediator"), O("기존 방식과 다른 점을 찾는다", "idea")),
        Q("나에게 좋은 리더는?", O("방향을 분명히 주는 사람", "leader"), O("근거와 기준이 명확한 사람", "analyst"), O("실행을 막지 않는 사람", "executor"), O("소통과 배려가 있는 사람", "mediator"), O("새 시도를 허용하는 사람", "idea")),
        Q("업무 개선안을 내라면?", O("조직 목표에 맞춰 제안한다", "leader"), O("데이터로 효과를 증명한다", "analyst"), O("바로 적용 가능한 안을 낸다", "executor"), O("사용자 불편을 줄이는 안을 낸다", "mediator"), O("완전히 새로운 방식을 낸다", "idea")),
        Q("마지막으로 내 업무 유형은?", O("방향을 잡는 사람", "leader"), O("근거를 세우는 사람", "analyst"), O("결과를 만드는 사람", "executor"), O("사람을 연결하는 사람", "mediator"), O("가능성을 여는 사람", "idea")),
        Q("업무 목표가 모호할 때?", O("결정권자에게 방향을 묻는다", "leader"), O("현재 자료로 가설을 세운다", "analyst"), O("작게 실행해 확인한다", "executor"), O("관련자 기대를 모은다", "mediator"), O("새로운 목표안을 제안한다", "idea")),
        Q("성과가 좋았던 프로젝트는?", O("리더십이 발휘된 프로젝트", "leader"), O("분석이 정확했던 프로젝트", "analyst"), O("실행 속도가 좋았던 프로젝트", "executor"), O("협업이 매끄러웠던 프로젝트", "mediator"), O("아이디어가 돋보였던 프로젝트", "idea")),
        Q("내가 보완하고 싶은 점은?", O("더 부드러운 설득", "leader"), O("더 빠른 실행", "analyst"), O("더 꼼꼼한 기록", "executor"), O("더 분명한 기준", "mediator"), O("더 확실한 마무리", "idea")),
    };

    private static List<TestQuestion> BuildMoneyQuestions() => new()
    {
        Q("월급이 들어오면 먼저 하는 일은?", O("저축과 고정비를 먼저 분리한다", "save"), O("필요한 지출 목록을 확인한다", "practical"), O("혜택과 조건을 비교한다", "smart"), O("나에게 작은 보상을 한다", "enjoy")),
        Q("비싼 물건을 살 때는?", O("오래 쓸 수 있는지 본다", "practical"), O("여러 쇼핑몰과 후기를 비교한다", "smart"), O("예산을 모을 때까지 기다린다", "save"), O("만족감이 크면 결제한다", "enjoy")),
        Q("할인 행사를 보면?", O("필요한 물건인지 먼저 본다", "practical"), O("평소 가격과 할인율을 확인한다", "smart"), O("예산 밖이면 참는다", "save"), O("기분 좋은 기회라면 산다", "enjoy")),
        Q("충동구매를 막는 방법은?", O("장바구니에 넣고 하루 기다린다", "smart"), O("월 예산 한도를 정한다", "save"), O("사용 빈도를 따져본다", "practical"), O("가끔은 즐거움 비용으로 인정한다", "enjoy")),
        Q("친구와 식사비를 낼 때?", O("상황에 맞게 기분 좋게 낸다", "enjoy"), O("각자 먹은 만큼 계산한다", "practical"), O("정산 앱으로 정확히 맞춘다", "smart"), O("이번 달 지출 계획을 고려한다", "save")),
        Q("여행 예산을 짤 때?", O("총액 한도를 먼저 정한다", "save"), O("숙소와 교통의 가성비를 본다", "practical"), O("항공권과 쿠폰을 비교한다", "smart"), O("특별한 경험에는 쓰고 싶다", "enjoy")),
        Q("가전제품 구매 기준은?", O("내구성과 기본 기능", "practical"), O("가격 변동과 리뷰 점수", "smart"), O("필요해도 예산을 맞춘 뒤 구매", "save"), O("생활 만족도가 올라가면 구매", "enjoy")),
        Q("카페 지출에 대해?", O("자주 가면 예산을 따로 둔다", "save"), O("가격 대비 양과 품질을 본다", "practical"), O("쿠폰과 적립을 챙긴다", "smart"), O("하루 기분 전환으로 필요하다", "enjoy")),
        Q("구독 서비스는?", O("쓰는 것만 남기고 정리한다", "practical"), O("혜택과 사용 횟수를 계산한다", "smart"), O("고정비라 신중히 가입한다", "save"), O("즐거우면 유지한다", "enjoy")),
        Q("새 옷을 살 때?", O("기존 옷과 잘 맞는지 본다", "practical"), O("가격 추이와 후기를 확인한다", "smart"), O("계절 예산 안에서 산다", "save"), O("마음에 들면 자신감 비용으로 본다", "enjoy")),
        Q("예상치 못한 수입이 생기면?", O("대부분 저축한다", "save"), O("필요했던 물건을 산다", "practical"), O("투자나 혜택 계좌를 검토한다", "smart"), O("맛있는 것과 경험에 쓴다", "enjoy")),
        Q("앱 결제나 인앱 구매는?", O("정말 필요한 기능인지 본다", "practical"), O("무료 대안과 비교한다", "smart"), O("작은 금액도 누적을 신경 쓴다", "save"), O("즐거우면 적당히 결제한다", "enjoy")),
        Q("선물 예산은?", O("미리 따로 준비한다", "save"), O("상대에게 실용적인 것을 고른다", "practical"), O("가격 대비 만족도가 높은 걸 찾는다", "smart"), O("기억에 남는 선물을 하고 싶다", "enjoy")),
        Q("배달음식 지출은?", O("횟수를 제한한다", "save"), O("집밥과 비교해 필요할 때만 시킨다", "practical"), O("쿠폰과 배달비를 확인한다", "smart"), O("힘든 날에는 충분히 가치 있다", "enjoy")),
        Q("보험이나 금융상품은?", O("안정성을 먼저 본다", "save"), O("필요 보장만 선택한다", "practical"), O("수수료와 조건을 비교한다", "smart"), O("복잡하면 미루는 편이다", "enjoy")),
        Q("중고거래는?", O("가성비가 좋으면 적극 활용한다", "practical"), O("시세와 상태를 꼼꼼히 비교한다", "smart"), O("새 제품보다 절약되면 선호한다", "save"), O("번거로우면 새 제품을 산다", "enjoy")),
        Q("마트에 가면?", O("목록에 있는 것만 산다", "save"), O("필요한 식재료 중심으로 산다", "practical"), O("단가와 행사 조합을 계산한다", "smart"), O("맛있어 보이면 추가로 산다", "enjoy")),
        Q("취미 비용은?", O("월 한도를 정해둔다", "save"), O("실제로 자주 할 것만 산다", "practical"), O("장비별 성능과 가격을 비교한다", "smart"), O("삶의 만족을 위한 투자다", "enjoy")),
        Q("가격이 오른 물건은?", O("대체품을 찾는다", "practical"), O("다른 판매처를 비교한다", "smart"), O("당분간 구매를 줄인다", "save"), O("꼭 원하면 받아들인다", "enjoy")),
        Q("포인트와 적립은?", O("꼼꼼히 챙긴다", "smart"), O("귀찮지 않은 범위에서 쓴다", "practical"), O("절약에 도움이 되니 중요하다", "save"), O("크게 신경 쓰지 않는다", "enjoy")),
        Q("월말에 지출을 보면?", O("다음 달 예산을 조정한다", "save"), O("불필요한 항목을 줄인다", "practical"), O("카테고리별로 분석한다", "smart"), O("즐거웠으면 괜찮다고 느낀다", "enjoy")),
        Q("신제품 출시를 보면?", O("지금 필요한지 판단한다", "practical"), O("초기 리뷰를 기다린다", "smart"), O("가격이 안정될 때까지 참는다", "save"), O("끌리면 빨리 써보고 싶다", "enjoy")),
        Q("외식 메뉴를 고를 때?", O("가격 대비 만족을 본다", "practical"), O("리뷰와 평점을 본다", "smart"), O("이번 주 외식 예산을 본다", "save"), O("먹고 싶은 마음을 우선한다", "enjoy")),
        Q("저축 목표는?", O("목표 금액과 기간을 정한다", "save"), O("생활에 무리 없는 선에서 한다", "practical"), O("이율과 상품 조건을 비교한다", "smart"), O("너무 빡빡하면 오래 못 간다", "enjoy")),
        Q("가성비와 만족 중 하나라면?", O("가성비가 기본이다", "practical"), O("수치로 비교해 결정한다", "smart"), O("예산 안이면 만족도도 본다", "save"), O("만족이 크면 가치 있다", "enjoy")),
        Q("현금과 카드 사용은?", O("지출 확인이 쉬운 방식을 쓴다", "smart"), O("혜택 좋은 카드를 활용한다", "practical"), O("과소비를 막는 방식을 택한다", "save"), O("편한 결제수단을 쓴다", "enjoy")),
        Q("큰 지출 후에는?", O("다른 지출을 줄인다", "save"), O("사용 만족도를 확인한다", "practical"), O("가격이 적절했는지 다시 본다", "smart"), O("기분 좋으면 후회하지 않는다", "enjoy")),
        Q("친구가 좋은 물건을 추천하면?", O("내게 필요한지 본다", "practical"), O("후기와 가격을 찾아본다", "smart"), O("예산 계획에 없으면 보류한다", "save"), O("끌리면 바로 써보고 싶다", "enjoy")),
        Q("비상금은?", O("반드시 필요하다", "save"), O("생활비 일부로 준비한다", "practical"), O("어디에 둘지 조건을 비교한다", "smart"), O("생각은 하지만 자주 쓰게 된다", "enjoy")),
        Q("시간 절약 서비스는?", O("시간 가치가 크면 쓴다", "practical"), O("비용 대비 효과를 계산한다", "smart"), O("자주 쓰면 예산을 따로 둔다", "save"), O("편해지면 충분히 좋다", "enjoy")),
        Q("내 소비 기록은?", O("가계부로 관리한다", "save"), O("큰 지출 위주로 본다", "practical"), O("앱으로 분류해 분석한다", "smart"), O("자세히 쓰면 답답하다", "enjoy")),
        Q("브랜드 제품은?", O("품질과 AS가 좋으면 산다", "practical"), O("동급 제품과 비교한다", "smart"), O("가격이 높으면 오래 고민한다", "save"), O("갖고 싶은 브랜드면 산다", "enjoy")),
        Q("공동구매를 보면?", O("필요한 물건이면 참여한다", "practical"), O("개별 구매가와 비교한다", "smart"), O("예정에 없으면 참는다", "save"), O("재미있고 싸면 참여한다", "enjoy")),
        Q("돈을 쓸 때 불편한 순간은?", O("예산을 넘길 때", "save"), O("쓸모가 불분명할 때", "practical"), O("비교가 충분하지 않을 때", "smart"), O("아끼느라 즐기지 못할 때", "enjoy")),
        Q("나에게 좋은 소비는?", O("미래 불안을 줄이는 소비", "save"), O("생활을 편하게 하는 소비", "practical"), O("조건이 가장 좋은 소비", "smart"), O("기분과 경험을 채우는 소비", "enjoy")),
        Q("온라인 쇼핑 장바구니는?", O("필요한 것만 담는다", "practical"), O("며칠 두고 가격을 본다", "smart"), O("예산을 넘으면 삭제한다", "save"), O("담아두는 것만으로도 즐겁다", "enjoy")),
        Q("가격 비교 시간이 길어지면?", O("기준을 정해 결론낸다", "smart"), O("시간도 비용이라 적당히 결정한다", "practical"), O("지출을 미뤄도 괜찮다", "save"), O("복잡하면 마음 가는 걸 산다", "enjoy")),
        Q("월 고정비를 볼 때?", O("줄일 수 있는 항목부터 본다", "save"), O("실제로 쓰는지 확인한다", "practical"), O("요금제 조건을 비교한다", "smart"), O("편리함을 주면 유지한다", "enjoy")),
        Q("부모님이나 가족 선물은?", O("실용적인 걸 고른다", "practical"), O("가격과 품질을 오래 비교한다", "smart"), O("미리 예산을 모은다", "save"), O("마음이 전해지면 아끼지 않는다", "enjoy")),
        Q("가끔 사치하고 싶을 때?", O("한도 안에서만 한다", "save"), O("오래 쓸 수 있으면 허용한다", "practical"), O("대체 상품까지 비교한다", "smart"), O("가끔은 나에게 필요하다", "enjoy")),
        Q("내 지출 패턴은?", O("계획적이고 조심스럽다", "save"), O("필요 중심으로 현실적이다", "practical"), O("비교와 분석이 많다", "smart"), O("기분과 경험에 열려 있다", "enjoy")),
        Q("할부 결제는?", O("가능하면 피한다", "save"), O("필요하면 현실적으로 쓴다", "practical"), O("무이자 조건을 확인한다", "smart"), O("부담이 분산되면 괜찮다", "enjoy")),
        Q("돈 관리 앱은?", O("예산 관리에 꼭 필요하다", "save"), O("큰 흐름만 볼 수 있으면 된다", "practical"), O("분석 그래프가 좋다", "smart"), O("너무 복잡하면 안 쓴다", "enjoy")),
        Q("복권이나 이벤트는?", O("작은 재미로만 본다", "enjoy"), O("큰 기대는 하지 않는다", "practical"), O("확률을 생각하면 잘 안 한다", "smart"), O("그 돈도 아껴야 한다", "save")),
        Q("가장 후회하는 소비는?", O("예산을 깨뜨린 소비", "save"), O("별로 쓰지 않은 물건", "practical"), O("더 싸게 살 수 있었던 물건", "smart"), O("즐겁지 않았던 경험 소비", "enjoy")),
        Q("가장 만족한 소비는?", O("목표 저축을 지킨 것", "save"), O("생활이 편해진 물건", "practical"), O("좋은 조건으로 산 물건", "smart"), O("오래 기억에 남는 경험", "enjoy")),
        Q("내 소비를 개선한다면?", O("예산을 더 명확히 한다", "save"), O("필요와 욕구를 구분한다", "practical"), O("비교 기준을 단순화한다", "smart"), O("만족도도 기록한다", "enjoy")),
        Q("마지막으로 내 소비 성향은?", O("미래를 먼저 지키는 편", "save"), O("쓸모를 먼저 보는 편", "practical"), O("조건을 따져보는 편", "smart"), O("현재의 행복도 보는 편", "enjoy")),
        Q("월급 전날 내 모습은?", O("다음 달 예산을 미리 세운다", "save"), O("필요 지출을 다시 확인한다", "practical"), O("카드 내역을 분석한다", "smart"), O("이번 달 즐거웠던 소비를 떠올린다", "enjoy")),
        Q("공짜 체험 이후 유료 전환은?", O("정말 쓰는지 확인한다", "practical"), O("다른 서비스와 비교한다", "smart"), O("고정비라 쉽게 전환하지 않는다", "save"), O("만족도가 크면 결제한다", "enjoy")),
    };

    private static List<TestQuestion> BuildTravelQuestions() => new()
    {
        Q("여행지를 고를 때 먼저 보는 것은?", O("동선과 교통 편의", "planner"), O("현지 분위기와 자유로움", "free"), O("대표 맛집과 시장", "foodie"), O("숙소와 휴식 환경", "healing"), O("새로운 체험 가능성", "explorer")),
        Q("여행 계획표는?", O("시간대별로 정리한다", "planner"), O("대략만 잡고 현장에서 정한다", "free"), O("식사 시간을 중심으로 짠다", "foodie"), O("쉬는 시간을 충분히 넣는다", "healing"), O("탐험할 후보지를 많이 적는다", "explorer")),
        Q("숙소 선택 기준은?", O("이동 동선이 좋은 곳", "planner"), O("동네 분위기가 재미있는 곳", "free"), O("맛집 접근성이 좋은 곳", "foodie"), O("조용하고 편안한 곳", "healing"), O("특이한 경험이 있는 곳", "explorer")),
        Q("여행 첫날에는?", O("예약 일정대로 움직인다", "planner"), O("가볍게 걸으며 감을 잡는다", "free"), O("현지 음식을 먼저 먹는다", "foodie"), O("숙소에서 천천히 쉰다", "healing"), O("랜드마크부터 도전한다", "explorer")),
        Q("길을 잃었을 때?", O("지도 앱으로 경로를 다시 잡는다", "planner"), O("그것도 여행이라 생각한다", "free"), O("근처 맛집을 찾아본다", "foodie"), O("카페에 들어가 쉬며 정리한다", "healing"), O("예상 못한 장소를 둘러본다", "explorer")),
        Q("여행 예산은?", O("항목별로 미리 나눈다", "planner"), O("큰 한도만 정한다", "free"), O("먹는 비용은 넉넉히 둔다", "foodie"), O("숙소와 휴식에는 투자한다", "healing"), O("체험과 입장료를 중시한다", "explorer")),
        Q("사진을 찍는 목적은?", O("기록과 일정 정리", "planner"), O("순간의 느낌 저장", "free"), O("음식과 메뉴 기록", "foodie"), O("풍경과 여유 기록", "healing"), O("새로운 장소 인증", "explorer")),
        Q("동행자와 의견이 다르면?", O("일정을 조정해 균형을 맞춘다", "planner"), O("각자 자유시간을 갖는다", "free"), O("식사 장소부터 합의한다", "foodie"), O("무리하지 않는 쪽을 택한다", "healing"), O("새로운 대안을 제안한다", "explorer")),
        Q("비가 오면?", O("실내 코스로 변경한다", "planner"), O("우산 쓰고 걸어본다", "free"), O("비 오는 날 맛집을 찾는다", "foodie"), O("숙소나 카페에서 쉰다", "healing"), O("비 오는 풍경도 즐긴다", "explorer")),
        Q("교통수단은?", O("시간표가 확실한 수단", "planner"), O("걷기와 대중교통을 섞는다", "free"), O("맛집 동선에 맞춘다", "foodie"), O("편하고 피로가 적은 수단", "healing"), O("현지 특색 있는 이동수단", "explorer")),
        Q("여행 전 준비물은?", O("체크리스트로 빠짐없이 챙긴다", "planner"), O("필수품만 챙기고 가볍게 간다", "free"), O("간식과 맛집 리스트를 챙긴다", "foodie"), O("편한 옷과 수면용품을 챙긴다", "healing"), O("액션캠이나 체험 장비를 챙긴다", "explorer")),
        Q("현지 시장에 가면?", O("동선을 확인하고 둘러본다", "planner"), O("발길 가는 대로 구경한다", "free"), O("먹거리부터 찾는다", "foodie"), O("사람 구경하며 천천히 걷는다", "healing"), O("처음 보는 물건과 장소를 탐색한다", "explorer")),
        Q("여행 중 가장 피하고 싶은 것은?", O("일정 꼬임", "planner"), O("빡빡한 통제", "free"), O("맛없는 식사", "foodie"), O("휴식 없는 강행군", "healing"), O("뻔한 코스만 도는 것", "explorer")),
        Q("일출 명소가 있다면?", O("시간 계산해 미리 도착한다", "planner"), O("일어나면 가고 아니면 쉰다", "free"), O("근처 아침 맛집도 찾는다", "foodie"), O("무리해서까지는 가지 않는다", "healing"), O("새벽 산행도 도전한다", "explorer")),
        Q("여행 후 기억에 남는 것은?", O("계획대로 잘 맞은 동선", "planner"), O("예상치 못한 순간", "free"), O("맛있었던 음식", "foodie"), O("편안했던 풍경과 쉼", "healing"), O("새롭게 해본 경험", "explorer")),
        Q("패키지 여행은?", O("효율적이면 좋다", "planner"), O("자유가 적으면 답답하다", "free"), O("식사가 괜찮은지 본다", "foodie"), O("편하면 만족한다", "healing"), O("특색 없으면 아쉽다", "explorer")),
        Q("렌터카 여행은?", O("경로와 주차를 미리 확인한다", "planner"), O("가는 길에 마음대로 멈춘다", "free"), O("지역 맛집을 연결한다", "foodie"), O("피곤하지 않게 이동한다", "healing"), O("숨은 장소까지 가본다", "explorer")),
        Q("여행 앱에서 먼저 찾는 기능은?", O("일정표와 지도", "planner"), O("주변 추천 장소", "free"), O("맛집 리뷰", "foodie"), O("숙소 후기와 편의시설", "healing"), O("액티비티와 체험", "explorer")),
        Q("기념품은?", O("미리 정한 사람 것만 산다", "planner"), O("마음에 들면 즉흥적으로 산다", "free"), O("지역 먹거리 선물을 산다", "foodie"), O("소박하고 오래 볼 것을 산다", "healing"), O("그 지역만의 독특한 것을 산다", "explorer")),
        Q("혼자 여행은?", O("계획만 잘 세우면 좋다", "planner"), O("가장 자유로워서 좋다", "free"), O("먹을 때 조금 아쉬울 수 있다", "foodie"), O("조용히 쉬기 좋다", "healing"), O("스스로 도전하는 느낌이 좋다", "explorer")),
        Q("가이드북을 본다면?", O("필수 코스와 시간 정보를 본다", "planner"), O("대략 느낌만 본다", "free"), O("음식 섹션을 먼저 본다", "foodie"), O("휴양지와 산책 코스를 본다", "healing"), O("숨은 명소를 찾는다", "explorer")),
        Q("항공권을 고를 때?", O("시간과 환승을 꼼꼼히 본다", "planner"), O("저렴하고 자유로운 일정이면 좋다", "free"), O("도착 후 식사 시간이 중요하다", "foodie"), O("피로가 적은 시간대를 고른다", "healing"), O("새 도시 경유도 흥미롭다", "explorer")),
        Q("여행 중 카페는?", O("동선 중간 휴식 지점", "planner"), O("마음에 들면 즉흥 방문", "free"), O("디저트 맛집이면 필수", "foodie"), O("오래 앉아 쉬는 공간", "healing"), O("특이한 콘셉트면 방문", "explorer")),
        Q("박물관이나 전시관은?", O("운영 시간과 위치를 확인한다", "planner"), O("끌리면 들어간다", "free"), O("근처 식사와 묶어 본다", "foodie"), O("조용히 보기 좋으면 간다", "healing"), O("새로운 지식을 얻으러 간다", "explorer")),
        Q("등산이나 트레킹은?", O("코스 난이도와 시간을 확인한다", "planner"), O("날씨 좋으면 가볍게 간다", "free"), O("하산 후 맛집이 중요하다", "foodie"), O("무리한 코스는 피한다", "healing"), O("새로운 코스를 도전한다", "explorer")),
        Q("바다 여행은?", O("숙소와 이동을 미리 잡는다", "planner"), O("해변 따라 자유롭게 걷는다", "free"), O("해산물 맛집을 찾는다", "foodie"), O("파도 소리 들으며 쉰다", "healing"), O("스노클링이나 보트를 해본다", "explorer")),
        Q("도시 여행은?", O("주요 명소를 순서대로 본다", "planner"), O("동네 골목을 걷는다", "free"), O("로컬 식당을 찾는다", "foodie"), O("공원과 카페에서 쉰다", "healing"), O("현지인이 가는 곳을 찾아본다", "explorer")),
        Q("여행 중 쇼핑은?", O("필요한 품목만 정해 산다", "planner"), O("마음에 드는 곳을 둘러본다", "free"), O("먹거리 쇼핑이 좋다", "foodie"), O("붐비면 오래 머물지 않는다", "healing"), O("전통시장이나 특색 매장을 찾는다", "explorer")),
        Q("체력 배분은?", O("일정표에 휴식 시간을 넣는다", "planner"), O("그날 컨디션에 맡긴다", "free"), O("식사 시간으로 회복한다", "foodie"), O("처음부터 무리하지 않는다", "healing"), O("힘들어도 새로운 경험이면 한다", "explorer")),
        Q("SNS 여행 후기는?", O("정보 중심으로 저장한다", "planner"), O("분위기만 참고한다", "free"), O("맛집 후기를 많이 본다", "foodie"), O("조용한 장소 후기를 본다", "healing"), O("특이한 코스를 찾는다", "explorer")),
        Q("로컬 축제가 있다면?", O("일정에 맞춰 넣는다", "planner"), O("우연히 만나면 즐긴다", "free"), O("먹거리 부스가 기대된다", "foodie"), O("사람이 너무 많으면 피한다", "healing"), O("그 지역 문화를 체험한다", "explorer")),
        Q("여행 중 아침은?", O("일정 시작 시간에 맞춰 먹는다", "planner"), O("늦잠 자고 가볍게 먹는다", "free"), O("유명한 아침 메뉴를 찾는다", "foodie"), O("숙소에서 여유롭게 먹는다", "healing"), O("일찍 나가 새로운 곳을 본다", "explorer")),
        Q("야경 명소는?", O("동선과 귀가 교통을 확인한다", "planner"), O("걷다 보이면 즐긴다", "free"), O("근처 야식도 찾는다", "foodie"), O("조용히 볼 수 있으면 좋다", "healing"), O("높은 전망대나 특별한 장소를 간다", "explorer")),
        Q("여행 중 변수는?", O("미리 대안 코스를 준비한다", "planner"), O("변수가 있어야 재미있다", "free"), O("식사 계획만은 지키고 싶다", "foodie"), O("피곤하면 쉬는 쪽으로 바꾼다", "healing"), O("예상 밖 경험을 기회로 본다", "explorer")),
        Q("동행자에게 듣고 싶은 말은?", O("일정 잘 짰다", "planner"), O("덕분에 자유롭고 편하다", "free"), O("맛집 정말 잘 골랐다", "foodie"), O("이번 여행 쉬기 좋다", "healing"), O("이런 곳은 처음이야", "explorer")),
        Q("여행지에서 가장 먼저 저장하는 정보는?", O("지도 위치와 영업시간", "planner"), O("끌리는 골목과 분위기", "free"), O("메뉴와 예약 정보", "foodie"), O("숙소 편의시설과 조용함", "healing"), O("체험 예약과 난이도", "explorer")),
        Q("여행 중 피로가 오면?", O("일정을 줄이고 조정한다", "planner"), O("잠깐 쉬고 다시 걷는다", "free"), O("맛있는 걸 먹고 회복한다", "foodie"), O("그날은 쉬는 날로 바꾼다", "healing"), O("가벼운 체험으로 전환한다", "explorer")),
        Q("지역 교통이 불편하면?", O("대체 경로를 미리 찾는다", "planner"), O("천천히 걸으며 즐긴다", "free"), O("맛집 주변으로 코스를 바꾼다", "foodie"), O("이동 적은 코스로 바꾼다", "healing"), O("오히려 숨은 장소를 찾는다", "explorer")),
        Q("여행 만족도를 좌우하는 것은?", O("일정의 안정성", "planner"), O("자유로운 흐름", "free"), O("음식의 만족도", "foodie"), O("회복과 편안함", "healing"), O("새로운 경험의 강도", "explorer")),
        Q("여행 중 기록은?", O("일정과 비용을 정리한다", "planner"), O("느낌 위주로 남긴다", "free"), O("먹은 메뉴를 기록한다", "foodie"), O("좋았던 풍경을 남긴다", "healing"), O("해본 활동을 기록한다", "explorer")),
        Q("비행기 지연이 생기면?", O("대체 일정을 계산한다", "planner"), O("공항에서 자유롭게 시간을 보낸다", "free"), O("공항 맛집을 찾는다", "foodie"), O("라운지나 조용한 곳을 찾는다", "healing"), O("근처 짧은 탐방을 고려한다", "explorer")),
        Q("호텔 조식은?", O("일정 시작 전 챙긴다", "planner"), O("그날 기분에 따라 먹는다", "free"), O("조식 품질도 숙소 선택 기준이다", "foodie"), O("여유롭게 먹는 시간이 좋다", "healing"), O("밖의 로컬 아침도 궁금하다", "explorer")),
        Q("현지 언어가 어렵다면?", O("번역 앱과 기본 문장을 준비한다", "planner"), O("몸짓과 분위기로 해본다", "free"), O("메뉴판 번역이 가장 중요하다", "foodie"), O("무리한 대화는 피한다", "healing"), O("새로운 소통도 경험이다", "explorer")),
        Q("여행 마지막 날은?", O("공항 시간에 맞춰 안정적으로 움직인다", "planner"), O("남은 시간까지 자유롭게 즐긴다", "free"), O("마지막 식사를 제대로 한다", "foodie"), O("무리하지 않고 천천히 정리한다", "healing"), O("마지막 숨은 코스를 하나 더 간다", "explorer")),
        Q("재방문하고 싶은 곳은?", O("동선이 편하고 안정적인 곳", "planner"), O("걷는 재미가 있는 곳", "free"), O("먹거리가 풍부한 곳", "foodie"), O("마음이 쉬는 곳", "healing"), O("아직 못 본 것이 많은 곳", "explorer")),
        Q("여행에서 나의 약점은?", O("계획이 틀어지면 불편하다", "planner"), O("준비 부족으로 당황할 수 있다", "free"), O("먹는 것에 일정이 치우친다", "foodie"), O("너무 쉬다 놓치는 게 있다", "healing"), O("무리해서 피곤해진다", "explorer")),
        Q("좋은 여행 앱이라면?", O("일정과 지도 관리가 좋아야 한다", "planner"), O("주변 추천이 자유로워야 한다", "free"), O("맛집 정보가 강해야 한다", "foodie"), O("조용한 장소와 숙소 정보가 좋아야 한다", "healing"), O("체험과 숨은 명소가 많아야 한다", "explorer")),
        Q("여행 후 가장 먼저 하는 일은?", O("사진과 비용을 정리한다", "planner"), O("기억나는 순간을 공유한다", "free"), O("맛집 후기를 남긴다", "foodie"), O("충분히 쉬며 여운을 느낀다", "healing"), O("다음 도전 여행을 찾는다", "explorer")),
        Q("마지막으로 내 여행은?", O("준비가 잘 된 여행", "planner"), O("흐름을 즐기는 여행", "free"), O("맛을 따라가는 여행", "foodie"), O("쉬기 위한 여행", "healing"), O("새로움을 찾는 여행", "explorer")),
        Q("여행 중 가장 아까운 시간은?", O("동선 실수로 낭비한 시간", "planner"), O("자유롭게 보지 못한 시간", "free"), O("맛없는 식사로 쓴 시간", "foodie"), O("쉴 수 없었던 시간", "healing"), O("새 경험 없이 지나간 시간", "explorer")),
    };

    private static List<TestQuestion> BuildFoodQuestions() => new()
    {
        Q("메뉴판을 볼 때 먼저 보는 것은?", O("익숙하고 편한 메뉴", "comfort"), O("처음 보는 메뉴", "adventure"), O("인기 표시와 후기", "trend"), O("맵고 진한 메뉴", "passion")),
        Q("새 식당에 가면?", O("대표 메뉴를 고른다", "comfort"), O("특이한 조합을 고른다", "adventure"), O("사진이 예쁜 메뉴를 본다", "trend"), O("맛이 강한 메뉴에 끌린다", "passion")),
        Q("스트레스 받은 날에는?", O("따뜻한 집밥 같은 음식", "comfort"), O("새로운 음식으로 기분 전환", "adventure"), O("유명 맛집 배달", "trend"), O("매운 음식이나 진한 국물", "passion")),
        Q("아침 식사는?", O("속 편한 밥이나 죽", "comfort"), O("색다른 브런치", "adventure"), O("요즘 인기 있는 베이커리", "trend"), O("든든하고 맛이 확실한 메뉴", "passion")),
        Q("야식 선택은?", O("라면이나 익숙한 메뉴", "comfort"), O("안 먹어본 배달 메뉴", "adventure"), O("리뷰 많은 인기 메뉴", "trend"), O("치킨, 곱창처럼 강한 메뉴", "passion")),
        Q("음식 사진을 찍는다면?", O("기록용으로 간단히 찍는다", "comfort"), O("새로운 음식이라 남긴다", "adventure"), O("예쁘게 찍어 공유한다", "trend"), O("먹음직한 느낌을 담는다", "passion")),
        Q("여행지 음식은?", O("실패 적은 유명 메뉴", "comfort"), O("현지인만 먹는 독특한 메뉴", "adventure"), O("SNS에서 본 맛집", "trend"), O("지역 특유의 강한 맛", "passion")),
        Q("친구가 추천한 음식은?", O("내 입맛에 맞을지 먼저 본다", "comfort"), O("궁금해서 먹어본다", "adventure"), O("후기를 더 찾아본다", "trend"), O("맛이 확실하면 바로 먹는다", "passion")),
        Q("뷔페에 가면?", O("좋아하는 음식부터 담는다", "comfort"), O("처음 보는 코너부터 간다", "adventure"), O("인기 많은 줄을 확인한다", "trend"), O("고기와 강한 메뉴부터 담는다", "passion")),
        Q("건강식에 대해?", O("속 편하면 좋다", "comfort"), O("새로운 건강 메뉴도 궁금하다", "adventure"), O("유행하는 샐러드나 포케를 본다", "trend"), O("건강식도 맛이 진해야 한다", "passion")),
        Q("매운 음식은?", O("적당한 매운맛이 좋다", "comfort"), O("특이한 매운 조합도 좋다", "adventure"), O("인기 매운맛 챌린지가 궁금하다", "trend"), O("맵고 강할수록 좋다", "passion")),
        Q("디저트는?", O("익숙한 케이크나 빵", "comfort"), O("새로운 맛 조합", "adventure"), O("사진 잘 나오는 디저트", "trend"), O("진한 초코나 치즈 맛", "passion")),
        Q("카페 음료는?", O("늘 마시는 커피", "comfort"), O("시즌 한정 메뉴", "adventure"), O("인기 신메뉴", "trend"), O("진하고 강한 맛", "passion")),
        Q("가정식 백반은?", O("가장 편하고 좋다", "comfort"), O("지역 반찬이 다르면 재미있다", "adventure"), O("유명 백반집이면 간다", "trend"), O("양념과 찌개 맛이 중요하다", "passion")),
        Q("퓨전 음식은?", O("너무 낯설면 망설인다", "comfort"), O("새로울수록 흥미롭다", "adventure"), O("요즘 뜨면 먹어본다", "trend"), O("맛의 임팩트가 있으면 좋다", "passion")),
        Q("식당 분위기는?", O("편안한 곳이 좋다", "comfort"), O("독특한 콘셉트가 좋다", "adventure"), O("사진 찍기 좋은 곳이 좋다", "trend"), O("음식 맛만 강하면 된다", "passion")),
        Q("혼밥 메뉴는?", O("늘 먹던 안정 메뉴", "comfort"), O("새 가게 탐방", "adventure"), O("평점 높은 곳", "trend"), O("한 그릇 강한 메뉴", "passion")),
        Q("회식 메뉴를 고른다면?", O("대부분 좋아하는 무난한 메뉴", "comfort"), O("새로운 곳을 제안한다", "adventure"), O("요즘 인기 있는 곳", "trend"), O("고기나 매운 메뉴", "passion")),
        Q("음식 리뷰는?", O("실패 방지용으로 본다", "comfort"), O("독특한 메뉴 정보가 좋다", "adventure"), O("평점과 사진을 꼼꼼히 본다", "trend"), O("맛 표현이 강한 리뷰를 본다", "passion")),
        Q("간편식은?", O("익숙한 브랜드가 좋다", "comfort"), O("신제품을 자주 산다", "adventure"), O("인기 제품을 따라 산다", "trend"), O("양념이 강한 제품을 산다", "passion")),
        Q("요리를 한다면?", O("검증된 레시피대로 한다", "comfort"), O("재료를 바꿔 실험한다", "adventure"), O("유튜브 인기 레시피를 따라한다", "trend"), O("양념을 넉넉히 쓴다", "passion")),
        Q("소스 선택은?", O("기본 소스가 좋다", "comfort"), O("처음 보는 소스를 고른다", "adventure"), O("인기 소스를 고른다", "trend"), O("매콤하거나 진한 소스", "passion")),
        Q("국물 음식은?", O("맑고 편안한 국물", "comfort"), O("낯선 향신료 국물", "adventure"), O("유명한 국물 맛집", "trend"), O("진하고 얼큰한 국물", "passion")),
        Q("면 요리는?", O("잔치국수나 우동처럼 편한 맛", "comfort"), O("분짜, 쌀국수 등 새 맛", "adventure"), O("핫한 라멘집", "trend"), O("짬뽕이나 탄탄면처럼 강한 맛", "passion")),
        Q("고기 메뉴는?", O("익숙한 삼겹살이나 불고기", "comfort"), O("특수부위나 이색 조리", "adventure"), O("예약 많은 고깃집", "trend"), O("양념 강한 고기", "passion")),
        Q("해산물은?", O("익숙한 생선구이", "comfort"), O("처음 보는 해산물", "adventure"), O("인기 횟집", "trend"), O("매운 해물찜", "passion")),
        Q("채소 메뉴는?", O("집밥 반찬처럼 편하면 좋다", "comfort"), O("새로운 샐러드 조합", "adventure"), O("유행하는 건강식", "trend"), O("강한 드레싱이 필요하다", "passion")),
        Q("향신료는?", O("은은한 정도가 좋다", "comfort"), O("낯선 향도 경험해본다", "adventure"), O("유행하는 향신료 음식은 궁금하다", "trend"), O("강한 향과 맛도 좋다", "passion")),
        Q("줄 서는 맛집은?", O("너무 길면 포기한다", "comfort"), O("새 경험이면 기다린다", "adventure"), O("유명하면 한 번은 간다", "trend"), O("맛이 확실하면 기다린다", "passion")),
        Q("실패한 메뉴를 만났을 때?", O("다음엔 익숙한 걸 먹는다", "comfort"), O("그래도 경험이라 생각한다", "adventure"), O("리뷰를 더 봤어야 했다고 생각한다", "trend"), O("맛이 약해서 아쉽다고 느낀다", "passion")),
        Q("배달앱을 켜면?", O("최근 주문한 메뉴를 본다", "comfort"), O("새로 생긴 가게를 본다", "adventure"), O("랭킹과 리뷰를 본다", "trend"), O("맵고 진한 메뉴를 찾는다", "passion")),
        Q("기념일 식사는?", O("편안한 단골집", "comfort"), O("새롭고 특별한 코스", "adventure"), O("예약 어려운 인기 레스토랑", "trend"), O("맛이 강하게 기억나는 곳", "passion")),
        Q("가격이 비싼 음식은?", O("익숙하고 확실해야 산다", "comfort"), O("새 경험이면 고려한다", "adventure"), O("유명하고 검증됐으면 간다", "trend"), O("맛의 존재감이 크면 가치 있다", "passion")),
        Q("음식 선택에서 중요한 것?", O("편안함", "comfort"), O("새로움", "adventure"), O("인기와 분위기", "trend"), O("강한 만족감", "passion")),
        Q("단골집은?", O("자주 가면 안정된다", "comfort"), O("너무 익숙하면 다른 곳도 간다", "adventure"), O("유명해지면 더 좋다", "trend"), O("맛이 변하면 바로 안다", "passion")),
        Q("친구와 메뉴 취향이 다르면?", O("무난한 공통 메뉴를 찾는다", "comfort"), O("상대 메뉴도 먹어본다", "adventure"), O("인기 많은 선택을 따른다", "trend"), O("내가 좋아하는 강한 메뉴를 설득한다", "passion")),
        Q("오늘 메뉴를 정하는 기준은?", O("속 편한 것", "comfort"), O("새로운 것", "adventure"), O("검색에 많이 나오는 것", "trend"), O("확 당기는 맛", "passion")),
        Q("지역 특산물은?", O("익숙한 재료면 먹는다", "comfort"), O("낯설수록 먹어본다", "adventure"), O("유명하면 구매한다", "trend"), O("맛이 진하면 좋다", "passion")),
        Q("음식 방송을 보면?", O("아는 음식이면 더 먹고 싶다", "comfort"), O("처음 보는 음식이 궁금하다", "adventure"), O("방송 맛집을 저장한다", "trend"), O("자극적인 장면에 끌린다", "passion")),
        Q("빵집에 가면?", O("소보로, 단팥처럼 익숙한 빵", "comfort"), O("처음 보는 신제품", "adventure"), O("인기 1위 메뉴", "trend"), O("버터나 크림이 진한 빵", "passion")),
        Q("피자를 고르면?", O("콤비네이션 같은 기본", "comfort"), O("새 토핑 조합", "adventure"), O("SNS 인기 피자", "trend"), O("치즈와 토핑이 강한 피자", "passion")),
        Q("한식/양식/중식 중에는?", O("익숙한 한식", "comfort"), O("그날 안 먹어본 종류", "adventure"), O("유명한 장르 맛집", "trend"), O("양념 강한 중식이나 한식", "passion")),
        Q("샐러드만 먹어야 한다면?", O("익숙한 닭가슴살 샐러드", "comfort"), O("새 드레싱을 시도한다", "adventure"), O("인기 포케를 고른다", "trend"), O("매콤한 토핑을 추가한다", "passion")),
        Q("라면을 끓인다면?", O("기본 조리법 그대로", "comfort"), O("색다른 재료를 넣는다", "adventure"), O("유행 레시피를 따라한다", "trend"), O("고추와 양념을 더한다", "passion")),
        Q("식후 만족감은?", O("속이 편해야 좋다", "comfort"), O("새로운 경험이면 좋다", "adventure"), O("분위기까지 좋으면 만족", "trend"), O("맛이 강하게 남아야 만족", "passion")),
        Q("음식 앱에 필요한 기능은?", O("내 단골 메뉴 저장", "comfort"), O("새 메뉴 추천", "adventure"), O("인기 순위와 사진", "trend"), O("매운맛/진한맛 필터", "passion")),
        Q("나의 음식 약점은?", O("새 메뉴 도전이 적다", "comfort"), O("실패 확률이 높다", "adventure"), O("유행에 영향을 받는다", "trend"), O("자극적인 맛을 자주 찾는다", "passion")),
        Q("다이어트 중이라면?", O("익숙한 건강 메뉴로 버틴다", "comfort"), O("새 건강식을 찾아본다", "adventure"), O("인기 식단을 참고한다", "trend"), O("맛이 약하면 오래 못 한다", "passion")),
        Q("마지막으로 내 음식 취향은?", O("편안하고 안정적인 맛", "comfort"), O("새롭고 낯선 맛", "adventure"), O("인기 있고 보기 좋은 맛", "trend"), O("강렬하고 확실한 맛", "passion")),
        Q("맛집 선택 실패를 줄이려면?", O("무난한 메뉴를 고른다", "comfort"), O("실패도 경험으로 본다", "adventure"), O("사진과 리뷰를 더 본다", "trend"), O("맛 강한 대표 메뉴를 고른다", "passion")),
    };

    private static List<TestQuestion> BuildColorQuestions() => new()
    {
        Q("끌리는 첫인상은?", O("강렬하고 자신감 있는 느낌", "red"), O("차분하고 믿음직한 느낌", "blue"), O("밝고 즐거운 느낌", "yellow"), O("편안하고 자연스러운 느낌", "green"), O("감성적이고 독특한 느낌", "purple")),
        Q("오늘 기분을 색으로 표현하면?", O("무언가 해내고 싶은 빨강", "red"), O("정리하고 싶은 파랑", "blue"), O("가볍고 웃고 싶은 노랑", "yellow"), O("쉬고 싶은 초록", "green"), O("상상에 빠지는 보라", "purple")),
        Q("중요한 발표 날에는?", O("존재감 있게 보이고 싶다", "red"), O("신뢰감 있게 보이고 싶다", "blue"), O("밝고 친근하게 보이고 싶다", "yellow"), O("부드럽고 안정적으로 보이고 싶다", "green"), O("개성 있게 기억되고 싶다", "purple")),
        Q("내 방 분위기는?", O("포인트가 강한 공간", "red"), O("정돈되고 차분한 공간", "blue"), O("밝고 경쾌한 공간", "yellow"), O("식물과 자연스러운 공간", "green"), O("감성 소품이 있는 공간", "purple")),
        Q("스트레스를 받을 때 필요한 것은?", O("에너지를 다시 올리는 자극", "red"), O("생각을 정리할 시간", "blue"), O("웃을 수 있는 가벼움", "yellow"), O("마음을 진정시키는 휴식", "green"), O("혼자 감정을 풀 창작 시간", "purple")),
        Q("친구에게 주는 느낌은?", O("든든하게 밀어주는 사람", "red"), O("믿고 맡길 수 있는 사람", "blue"), O("분위기를 밝히는 사람", "yellow"), O("편안하게 들어주는 사람", "green"), O("생각이 깊고 특별한 사람", "purple")),
        Q("갈등이 생기면?", O("정면으로 해결한다", "red"), O("근거와 기준을 세운다", "blue"), O("분위기를 부드럽게 바꾼다", "yellow"), O("서로 편한 타협점을 찾는다", "green"), O("감정의 의미를 오래 생각한다", "purple")),
        Q("새로운 시작 앞에서?", O("바로 도전하고 싶다", "red"), O("준비와 계획이 필요하다", "blue"), O("기대와 호기심이 크다", "yellow"), O("무리하지 않게 시작하고 싶다", "green"), O("나만의 방식으로 해보고 싶다", "purple")),
        Q("옷을 고를 때?", O("눈에 띄는 포인트", "red"), O("단정하고 안정적인 톤", "blue"), O("밝고 산뜻한 느낌", "yellow"), O("편안하고 자연스러운 색", "green"), O("감각적이고 개성 있는 색", "purple")),
        Q("일할 때 필요한 분위기?", O("집중을 끌어올리는 긴장감", "red"), O("차분한 질서와 기준", "blue"), O("가벼운 대화와 활기", "yellow"), O("안정적인 협업 분위기", "green"), O("영감을 주는 자유로움", "purple")),
        Q("좋아하는 카페는?", O("강렬한 콘셉트 카페", "red"), O("조용하고 깔끔한 카페", "blue"), O("햇살 좋고 밝은 카페", "yellow"), O("식물 많고 편한 카페", "green"), O("음악과 조명이 감성적인 카페", "purple")),
        Q("내가 듣고 싶은 말은?", O("너 정말 추진력 있다", "red"), O("너라면 믿을 수 있어", "blue"), O("너랑 있으면 즐거워", "yellow"), O("너랑 있으면 편안해", "green"), O("너만의 분위기가 있어", "purple")),
        Q("프로필 이미지를 고른다면?", O("강한 인상을 주는 사진", "red"), O("깔끔하고 신뢰감 있는 사진", "blue"), O("웃는 밝은 사진", "yellow"), O("자연스럽고 편한 사진", "green"), O("감성적이고 독특한 사진", "purple")),
        Q("비 오는 날 느낌은?", O("답답해서 움직이고 싶다", "red"), O("차분히 정리하기 좋다", "blue"), O("실내에서 즐길 일을 찾는다", "yellow"), O("조용히 쉬기 좋다", "green"), O("감성에 잠기기 좋다", "purple")),
        Q("햇살 좋은 날에는?", O("밖으로 나가 활동한다", "red"), O("계획한 일을 처리한다", "blue"), O("사람들과 만나고 싶다", "yellow"), O("산책하며 여유를 느낀다", "green"), O("사진이나 글감을 떠올린다", "purple")),
        Q("선물 포장은?", O("강렬한 포인트 리본", "red"), O("단정한 포장", "blue"), O("밝고 귀여운 포장", "yellow"), O("자연스럽고 따뜻한 포장", "green"), O("감성적이고 특별한 포장", "purple")),
        Q("내 에너지 사용 방식은?", O("몰아서 강하게 쓴다", "red"), O("계획적으로 안정적으로 쓴다", "blue"), O("즐거운 일에 가볍게 쓴다", "yellow"), O("천천히 오래 유지한다", "green"), O("영감이 올 때 깊게 쓴다", "purple")),
        Q("선호하는 앱 테마는?", O("강한 포인트 컬러", "red"), O("깔끔한 블루 톤", "blue"), O("밝고 귀여운 톤", "yellow"), O("편안한 그린 톤", "green"), O("감성적인 퍼플 톤", "purple")),
        Q("내가 불편한 분위기는?", O("너무 답답하고 느린 분위기", "red"), O("기준 없이 흐트러진 분위기", "blue"), O("무겁고 말 없는 분위기", "yellow"), O("갈등이 심한 분위기", "green"), O("개성이 무시되는 분위기", "purple")),
        Q("휴식 방식은?", O("운동이나 활동으로 푼다", "red"), O("정리하고 계획을 세운다", "blue"), O("재미있는 콘텐츠를 본다", "yellow"), O("자연 속에서 쉰다", "green"), O("음악, 글, 그림에 빠진다", "purple")),
        Q("좋아하는 문구는?", O("일단 해보자", "red"), O("차분히 확인하자", "blue"), O("재밌게 해보자", "yellow"), O("무리하지 말자", "green"), O("다르게 바라보자", "purple")),
        Q("관계에서 중요한 것은?", O("솔직한 표현", "red"), O("신뢰와 약속", "blue"), O("즐거운 소통", "yellow"), O("배려와 안정", "green"), O("깊은 이해", "purple")),
        Q("선택을 앞두면?", O("마음이 끌리는 쪽으로 간다", "red"), O("근거가 충분한 쪽을 택한다", "blue"), O("즐거워 보이는 쪽을 택한다", "yellow"), O("모두가 편한 쪽을 택한다", "green"), O("의미 있는 쪽을 택한다", "purple")),
        Q("나의 개성은?", O("강한 에너지", "red"), O("차분한 신뢰감", "blue"), O("밝은 호기심", "yellow"), O("따뜻한 균형감", "green"), O("깊은 감성", "purple")),
        Q("집중이 필요할 때?", O("짧고 강하게 몰입한다", "red"), O("조용히 순서대로 한다", "blue"), O("즐거운 요소를 넣는다", "yellow"), O("편안한 환경을 만든다", "green"), O("영감이 떠오를 시간을 둔다", "purple")),
        Q("운동을 한다면?", O("강도 높은 운동", "red"), O("루틴이 정해진 운동", "blue"), O("즐거운 그룹 운동", "yellow"), O("요가나 산책", "green"), O("댄스나 표현적인 운동", "purple")),
        Q("여행 색감은?", O("강렬한 도시와 야경", "red"), O("푸른 바다와 하늘", "blue"), O("햇살과 밝은 거리", "yellow"), O("숲과 자연", "green"), O("노을과 감성적인 골목", "purple")),
        Q("내가 화날 때는?", O("바로 표현하고 싶다", "red"), O("이유를 정리한다", "blue"), O("금방 풀리기도 한다", "yellow"), O("갈등을 줄이고 싶다", "green"), O("속으로 오래 생각한다", "purple")),
        Q("사람들이 모인 자리에서?", O("존재감을 드러낸다", "red"), O("차분히 필요한 말을 한다", "blue"), O("웃음을 만든다", "yellow"), O("분위기를 편하게 한다", "green"), O("조용히 인상적인 말을 한다", "purple")),
        Q("새 물건을 고를 때?", O("눈에 띄는 디자인", "red"), O("내구성과 신뢰감", "blue"), O("귀엽고 밝은 디자인", "yellow"), O("자연스럽고 편한 디자인", "green"), O("희소하고 감각적인 디자인", "purple")),
        Q("내 마음이 안정되는 순간은?", O("목표를 향해 움직일 때", "red"), O("정리가 끝났을 때", "blue"), O("웃고 떠들 때", "yellow"), O("평화로운 시간을 보낼 때", "green"), O("내 감정을 표현했을 때", "purple")),
        Q("창의력이 필요하면?", O("강한 자극을 찾는다", "red"), O("자료를 모아 구조화한다", "blue"), O("사람들과 대화한다", "yellow"), O("산책하며 여유를 둔다", "green"), O("혼자 깊게 상상한다", "purple")),
        Q("모바일 배경화면은?", O("에너지 있는 이미지", "red"), O("깔끔한 단색이나 풍경", "blue"), O("밝고 귀여운 이미지", "yellow"), O("자연 풍경", "green"), O("감성 사진이나 일러스트", "purple")),
        Q("내가 주고 싶은 영향은?", O("용기와 추진력", "red"), O("신뢰와 안정", "blue"), O("웃음과 긍정", "yellow"), O("편안함과 조화", "green"), O("영감과 감성", "purple")),
        Q("도전 과제가 주어지면?", O("경쟁심이 생긴다", "red"), O("계획부터 세운다", "blue"), O("재미있게 접근한다", "yellow"), O("무리하지 않게 조절한다", "green"), O("나만의 해석을 더한다", "purple")),
        Q("색을 하나만 고른다면 이유는?", O("힘이 나서", "red"), O("차분해서", "blue"), O("밝아서", "yellow"), O("편해서", "green"), O("특별해서", "purple")),
        Q("감동받는 순간은?", O("누군가 용기 있게 행동할 때", "red"), O("약속을 지켜줄 때", "blue"), O("함께 웃을 때", "yellow"), O("조용히 배려받을 때", "green"), O("말로 설명하기 어려운 분위기를 느낄 때", "purple")),
        Q("일상의 균형은?", O("활동과 성취가 있어야 한다", "red"), O("계획과 안정이 있어야 한다", "blue"), O("즐거움과 만남이 있어야 한다", "yellow"), O("쉼과 관계가 조화로워야 한다", "green"), O("감성과 표현이 있어야 한다", "purple")),
        Q("좋아하는 계절 느낌은?", O("뜨거운 여름의 에너지", "red"), O("맑은 겨울의 차분함", "blue"), O("봄 햇살의 밝음", "yellow"), O("초여름 숲의 편안함", "green"), O("가을 노을의 감성", "purple")),
        Q("팀에서 필요한 색이라면?", O("추진하는 빨강", "red"), O("정리하는 파랑", "blue"), O("활기를 주는 노랑", "yellow"), O("조율하는 초록", "green"), O("영감을 주는 보라", "purple")),
        Q("내가 지치기 쉬운 때는?", O("아무것도 못 하고 멈춰 있을 때", "red"), O("기준이 흔들릴 때", "blue"), O("재미가 없을 때", "yellow"), O("갈등이 많을 때", "green"), O("감정이 막혀 있을 때", "purple")),
        Q("좋은 하루의 색은?", O("성과가 있는 빨강", "red"), O("정돈된 파랑", "blue"), O("웃음 많은 노랑", "yellow"), O("평온한 초록", "green"), O("영감 있는 보라", "purple")),
        Q("나를 표현하는 키워드는?", O("열정", "red"), O("신뢰", "blue"), O("긍정", "yellow"), O("균형", "green"), O("감성", "purple")),
        Q("색깔 테스트 결과에서 기대하는 것은?", O("나의 에너지를 알고 싶다", "red"), O("나의 안정감을 알고 싶다", "blue"), O("나의 밝은 면을 알고 싶다", "yellow"), O("나의 관계 방식을 알고 싶다", "green"), O("나의 개성을 알고 싶다", "purple")),
        Q("마지막으로 가장 가까운 문장은?", O("나는 움직이며 힘을 얻는다", "red"), O("나는 차분할 때 강해진다", "blue"), O("나는 즐거울 때 빛난다", "yellow"), O("나는 편안할 때 오래 간다", "green"), O("나는 감성이 살아야 나답다", "purple")),
        Q("하루를 시작할 때 필요한 색감은?", O("의욕을 올리는 강렬함", "red"), O("마음을 정리하는 차분함", "blue"), O("기분을 띄우는 밝음", "yellow"), O("긴장을 낮추는 자연스러움", "green"), O("상상력을 깨우는 감성", "purple")),
        Q("사람들과 협업할 때 나는?", O("속도와 결정을 만든다", "red"), O("기준과 신뢰를 만든다", "blue"), O("분위기와 활기를 만든다", "yellow"), O("균형과 배려를 만든다", "green"), O("새 관점과 영감을 만든다", "purple")),
        Q("나를 상징하는 공간은?", O("강한 포인트가 있는 무대", "red"), O("정돈된 작업실", "blue"), O("햇살 좋은 거실", "yellow"), O("식물이 있는 쉼터", "green"), O("조명 있는 감성 방", "purple")),
        Q("중요한 결정을 앞두면?", O("과감하게 선택한다", "red"), O("근거를 확인한다", "blue"), O("즐거운 가능성을 본다", "yellow"), O("모두에게 무리 없는지 본다", "green"), O("내 마음의 의미를 본다", "purple")),
        Q("내 색깔이 강해지는 순간은?", O("목표가 생겼을 때", "red"), O("책임질 일이 있을 때", "blue"), O("사람들과 웃을 때", "yellow"), O("누군가를 도울 때", "green"), O("혼자 깊이 느낄 때", "purple")),
    };
    private static Dictionary<string, TestResult> BuildMbtiResults()
    {
        return new Dictionary<string, TestResult>
        {
            ["ENFP"] = R("ENFP 활동가형", "ENFP", "🦋", "상상력과 에너지가 강한 자유형 아이디어러입니다.", "아이디어를 작은 실행 계획으로 연결하면 더 빛납니다."),
            ["ENTP"] = R("ENTP 발명가형", "ENTP", "🐠", "새로운 관점으로 답을 찾는 재치형 탐험가입니다.", "논쟁보다 설득과 마무리를 챙기면 좋습니다."),
            ["INFJ"] = R("INFJ 조언가형", "INFJ", "🌳", "조용하지만 핵심을 보는 통찰형 성향입니다.", "혼자 책임지기보다 도움을 요청해도 좋습니다."),
            ["INFP"] = R("INFP 몽상가형", "INFP", "🎠", "따뜻한 가치관을 가진 섬세한 이상주의자입니다.", "생각을 현실의 작은 행동으로 옮겨보세요."),
            ["ENFJ"] = R("ENFJ 리더형", "ENFJ", "🦉", "사람의 마음을 움직이는 따뜻한 리더입니다.", "타인을 챙기는 만큼 나의 회복도 챙기세요."),
            ["ENTJ"] = R("ENTJ 전략가형", "ENTJ", "🐉", "목표를 구조화하고 밀어붙이는 추진형입니다.", "속도와 함께 팀의 공감도 확인하면 좋습니다."),
            ["INTJ"] = R("INTJ 설계자형", "INTJ", "🔥", "큰 그림과 계획에 강한 미래 설계자입니다.", "계획을 공유하면 주변의 협력을 얻기 쉽습니다."),
            ["INTP"] = R("INTP 탐구자형", "INTP", "🚴", "궁금증을 끝까지 파고드는 논리 탐험가입니다.", "분석 후 실행 마감선을 정하면 성과가 커집니다."),
            ["ESFP"] = R("ESFP 에너지형", "ESFP", "🎉", "분위기를 밝게 만드는 현장형 에너지러입니다.", "즐거움 속에서도 중요한 약속은 기록해두세요."),
            ["ESTP"] = R("ESTP 실행가형", "ESTP", "🐺", "바로 움직이며 답을 찾는 현실형 실행가입니다.", "빠른 판단 전에 한 번만 리스크를 확인하세요."),
            ["ISFP"] = R("ISFP 예술가형", "ISFP", "🍃", "감성과 자유로움을 가진 조용한 예술가입니다.", "표현을 미루지 않으면 관계가 더 편해집니다."),
            ["ISTP"] = R("ISTP 문제해결형", "ISTP", "🛠️", "원리를 파악해 해결하는 실전 문제 해결사입니다.", "혼자 해결하기 어려운 일은 역할 분담을 활용하세요."),
            ["ESFJ"] = R("ESFJ 사교형", "ESFJ", "👵", "주변을 세심하게 챙기는 다정한 운영자입니다.", "모두를 챙기려다 지치지 않게 경계를 세우세요."),
            ["ESTJ"] = R("ESTJ 관리자형", "ESTJ", "🏡", "정리와 실행에 강한 현실형 관리자입니다.", "기준을 말할 때 상대의 감정도 함께 살피면 좋습니다."),
            ["ISFJ"] = R("ISFJ 보호자형", "ISFJ", "🧹", "조용하지만 든든한 배려형 보호자입니다.", "나의 요구도 분명히 말하는 연습이 필요합니다."),
            ["ISTJ"] = R("ISTJ 신뢰형", "ISTJ", "⏰", "약속과 기준을 지키는 신뢰형 관리자입니다.", "변화가 필요할 때는 작은 실험부터 시작해보세요.")
        };
    }

    private static TestQuestion Q(string text, params TestOption[] options) => new(text, options.ToList());
    private static TestOption O(string text, string scoreKey, int score = 1) => new(text, scoreKey, score);
    private static TestResult R(string title, string shortName, string emoji, string description, string advice) => new(title, shortName, emoji, description, advice);
}

public sealed record PersonalityTest(
    string Key,
    string Icon,
    string Title,
    string Badge,
    List<TestQuestion> Questions,
    Dictionary<string, TestResult> Results);

public sealed record TestQuestion(string Text, List<TestOption> Options);
public sealed record TestOption(string Text, string ScoreKey, int Score = 1);
public sealed record TestResult(string Title, string ShortName, string Emoji, string Description, string Advice);
