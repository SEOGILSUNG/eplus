# 성격어때 MAUI Android

## 추가 반영
- 결과 화면에 수신 휴대폰 번호 기본 표시
- 번호 수정 가능
- `톡 보내기` 버튼 추가
- 전송 내용은 결과 + 상세분석 전체 텍스트
- frmKakao/CommonMsgSender 구조를 MAUI용 HttpClient 방식으로 변환

## 설정 필요
`MainPage.xaml.cs`에서 아래 값을 실제 값으로 변경하세요.

```csharp
private const string TalkAuthKey = "YOUR_AUTH_KEY";
```

보안상 운영 배포 앱에는 AuthKey를 직접 넣지 말고 Flask/API 서버를 통해 전송하는 방식을 권장합니다.

## Android 설정
알림톡 API 주소가 HTTP라서 AndroidManifest.xml에 아래 설정을 포함했습니다.

```xml
android:usesCleartextTraffic="true"
```

## 빌드 전 정리
```bat
dotnet clean
rd /s /q bin
rd /s /q obj
dotnet publish -f net9.0-android -c Release
```


## 이번 수정
- NormalizePhoneNumber 함수 추가
- NormalizePhone 기존 호출도 유지
- +82 번호를 010 형식으로 보정
- 전화번호 표시용 FormatPhoneNumber 함수 추가
- Android 8.0 미만에서는 READ_PHONE_NUMBERS 권한 제외


## 이번 수정 추가
- MainPage.xaml.cs 전화번호 조회 함수를 `PhoneStatePermission` 기준으로 교체
- Android 버전별 전화번호 조회 분기 적용
- `GetAndroidPhoneNumberSafe()` 추가
- `NormalizePhoneNumber()` 호출까지 포함
- 결과 화면에서 조회 실패 시 직접 입력 안내 표시
