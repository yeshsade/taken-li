# taken-li
שכחת את המקלדת על מצב אנגלית. הכל בסדר

כלי מקומי ל־Windows, שרץ ליד השעון ומתקן טקסט במקום שבו מקלידים. פועל ללא אינטרנט ומשתמש רק בפריסות המקלדת שכבר מותקנות.

## שימוש

להורדת קובץ שנבנה ב־GitHub: פתח את לשונית **Actions**, בחר ריצה מוצלחת של **Build Windows application**, ובאזור **Artifacts** הורד את **TakenLi-windows-x64**. חלץ את קובץ ה־ZIP והפעל את `TakenLi.exe`. נדרשת כניסה ל־GitHub והרשאה למאגר. כתובת `/workspace/...` מתוך שיחת הענן אינה קישור הורדה למחשב.

העתק את `TakenLi.exe` לתיקייה קבועה במחשב Windows והפעל אותו. גרסת ההפצה כוללת את סביבת הריצה; אין צורך להתקין .NET או להוריד מאגרים. בהפעלה מוצג הסבר שניתן להסתיר ולפתוח שוב מהסמל ליד השעון.

| פעולה | מקש התחלתי |
| --- | --- |
| תיקון עברית ואנגלית לפי המקשים, כולל טקסט מעורב | F10 |
| החלפת כל אות גדולה לקטנה ולהפך | Shift+F10 |
| היפוך סדר האותיות בכל שורה בנפרד | F6 |

סמן טקסט והפעל את הפעולה. אם אין סימון, אפשר לבחור בהגדרות בין השורה הנוכחית לבין כל השדה. ניתן להפעיל פעולות גם מתפריט הסמל ולשנות את מקשיהן.

המקלדת חוזרת לעברית אחרי 30 שניות ללא הקלדה באנגלית. כל הקלדה נוספת מתחילה את ההמתנה מחדש. אפשר לשנות את הזמן, לכבות את האפשרות ולהחריג תוכנות.

אם LangOver פועלת עם אותם מקשים, סגור אותה או בחר מקשים אחרים. התוכנה תציג שגיאה במקרה של התנגשות. אין הבטחה שכל יישום תומך באוטומציה; בדיקות Windows עדיין נדרשות לפי [רשימת בדיקות הקבלה](WINDOWS-VALIDATION.md).

הגדרות המשתמש נשמרות ב־`%LOCALAPPDATA%\TakenLi\settings.json`, ללא היסטוריית טקסטים. ״להפעיל עם Windows״ מוסיף רישום עבור המשתמש הנוכחי בלבד. לפני מחיקת קובץ ההפעלה, בטל אפשרות זו וצא מהתוכנה. אין להעתיק את הקובץ למיקום אחר בזמן שהרישום עדיין מצביע למיקום הישן.

## פיתוח ובנייה

נדרש SDK של .NET 10 לפי `global.json`. הקוד מורכב מליבת המרה שניתנת לבדיקה בכל מערכת ומיישום WinForms עם שילוב Windows.

```sh
dotnet run --project tests/TakenLi.Core.Checks --configuration Release
dotnet build src/TakenLi.Windows/TakenLi.Windows.csproj --configuration Release
dotnet publish src/TakenLi.Windows/TakenLi.Windows.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --output artifacts/win-x64
```

אפשר לבנות את קובץ Windows גם ב־Linux, אבל להריץ את היישום ולבדוק החלפת טקסט בתוכנות אחרות יש ב־Windows. `scripts/Publish.ps1` מריץ בדיקות ומפיק קובץ הפצה; ניתן לבחור בו גם `win-arm64`. תהליך GitHub Actions בונה קובץ x64 ומריץ בדיקות פריסה של חלונות Windows בעברית ובאנגלית. תצלומי החלונות נשמרים בתוצר נפרד **TakenLi-native-ui-checks**; קובץ ההפעלה בתוצר **TakenLi-windows-x64**.

בדיקות ממשק על Windows: `dotnet run --project tests/TakenLi.Windows.Checks --configuration Release -- artifacts/ui-checks`. נוסח ההסבר המאושר נשמר ב־[WELCOME-COPY.md](WELCOME-COPY.md).

מפרט ההתנהגות: [REQUIREMENTS.md](REQUIREMENTS.md). מפרט העיצוב שאושר: [DESIGN.md](DESIGN.md). ההדמיות ב־`design-exploration` נועדו להשוואת עיצוב בלבד; הן אינן ממשק היישום.
