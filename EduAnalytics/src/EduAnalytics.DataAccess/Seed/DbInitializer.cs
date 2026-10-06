using EduAnalytics.Core.Entities;
using EduAnalytics.Core.Enums;
using EduAnalytics.DataAccess.Context;

namespace EduAnalytics.DataAccess.Seed;

/// <summary>
/// Demo/test modu: uygulama açılışında veritabanını sıfırlar ve
/// Yazılım Kalite ve Test dersi için tam analiz yapılabilecek örnek veri üretir.
/// Gerçek kullanıma geçerken ResetDatabaseOnStartup false yapılmalıdır.
/// </summary>
public static class DbInitializer
{
    private static readonly bool ResetDatabaseOnStartup = false;

    public static void Seed(EduAnalyticsDbContext context)
    {
        if (ResetDatabaseOnStartup)
            context.Database.EnsureDeleted();

        context.Database.EnsureCreated();

        if (context.Exams.Any())
            return;

        var teacher = new User
        {
            FullName = "Demo Öğretmen",
            Email = "ogretmen@edu.tr",
            PasswordHash = "SHA256_PLACEHOLDER_NOT_REAL",
            Role = UserRole.Teacher,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(teacher);
        context.SaveChanges();

        var program = new Program
        {
            Name = "Yazılım Mühendisliği",
            Code = "YZM"
        };
        context.Programs.Add(program);
        context.SaveChanges();

        var programOutcomes = new[]
        {
            "Yazılım mühendisliği problemlerini tanımlama, modelleme ve çözme becerisi",
            "Yazılım yaşam döngüsü süreçlerini kalite odaklı planlama ve yürütme becerisi",
            "Doğrulama, geçerleme ve test tekniklerini uygun bağlamda uygulama becerisi",
            "Test otomasyonu, hata izleme ve kalite metrikleri için modern araçları kullanma becerisi",
            "Deney, ölçüm ve test verilerini analiz ederek sonuç yorumlama becerisi",
            "Takım çalışması, teknik raporlama ve mesleki etik farkındalığı"
        }.Select((description, i) => new ProgramOutcome
        {
            ProgramId = program.Id,
            Code = $"PÇ-{i + 1}",
            Description = description
        }).ToList();
        context.ProgramOutcomes.AddRange(programOutcomes);
        context.SaveChanges();

        var course = new Course
        {
            ProgramId = program.Id,
            Code = "YKT301",
            Name = "Yazılım Kalite ve Test",
            CreatedByUserId = teacher.Id
        };
        context.Courses.Add(course);
        context.SaveChanges();

        var topics = new[]
        {
            (1, "Yazılım kalitesi ve kalite modelleri"),
            (2, "Test süreci, seviye ve türleri"),
            (3, "Beyaz kutu ve siyah kutu test teknikleri"),
            (4, "Birim test ve test otomasyonu"),
            (5, "Hata yönetimi, regresyon ve kalite metrikleri"),
            (6, "Test raporlama ve güvenilirlik yorumlama")
        }.Select(t => new Topic
        {
            CourseId = course.Id,
            WeekNumber = t.Item1,
            Title = t.Item2,
            Description = t.Item2
        }).ToList();
        context.Topics.AddRange(topics);
        context.SaveChanges();

        var learningOutcomes = new[]
        {
            ("ÖÇ-1", "Kalite kavramlarını açıklar", "Yazılım kalite güvencesi, kalite modeli ve kalite maliyeti kavramlarını açıklar."),
            ("ÖÇ-2", "Test sürecini planlar", "Test seviyelerini, test türlerini ve test planı bileşenlerini ayırt eder."),
            ("ÖÇ-3", "Test tekniklerini uygular", "Siyah kutu ve beyaz kutu test tasarım tekniklerini örnek problem üzerinde uygular."),
            ("ÖÇ-4", "Otomasyon ve araçları yorumlar", "Birim test, test otomasyonu, hata izleme ve regresyon süreçlerini değerlendirir."),
            ("ÖÇ-5", "Metriklerle analiz yapar", "Test sonuçlarını, kalite metriklerini ve güvenilirlik bulgularını yorumlar.")
        }.Select(lo => new LearningOutcome
        {
            CourseId = course.Id,
            Code = lo.Item1,
            Name = lo.Item2,
            Description = lo.Item3
        }).ToList();
        context.LearningOutcomes.AddRange(learningOutcomes);
        context.SaveChanges();

        context.TopicLearningOutcomes.AddRange(new[]
        {
            LinkTopicLo(topics[0], learningOutcomes[0]),
            LinkTopicLo(topics[1], learningOutcomes[1]),
            LinkTopicLo(topics[2], learningOutcomes[2]),
            LinkTopicLo(topics[3], learningOutcomes[3]),
            LinkTopicLo(topics[4], learningOutcomes[3]),
            LinkTopicLo(topics[4], learningOutcomes[4]),
            LinkTopicLo(topics[5], learningOutcomes[4])
        });

        var matrix = new int[,]
        {
            { 4, 5, 2, 1, 2, 3 },
            { 3, 5, 4, 2, 3, 3 },
            { 4, 3, 5, 3, 4, 2 },
            { 2, 4, 4, 5, 3, 3 },
            { 3, 3, 4, 4, 5, 4 }
        };

        var mappings = new List<ProgramOutcomeMapping>();
        for (var loIndex = 0; loIndex < learningOutcomes.Count; loIndex++)
        {
            for (var poIndex = 0; poIndex < programOutcomes.Count; poIndex++)
            {
                mappings.Add(new ProgramOutcomeMapping
                {
                    LearningOutcomeId = learningOutcomes[loIndex].Id,
                    ProgramOutcomeId = programOutcomes[poIndex].Id,
                    ContributionLevel = matrix[loIndex, poIndex]
                });
            }
        }
        context.ProgramOutcomeMappings.AddRange(mappings);
        context.SaveChanges();

        var students = Enumerable.Range(1, 18)
            .Select(i => new Student
            {
                StudentNumber = $"2024{1000 + i}",
                FullName = DemoStudentNames[i - 1],
                ClassName = i <= 9 ? "Yazılım Mühendisliği 3A" : "Yazılım Mühendisliği 3B"
            })
            .ToList();
        context.Students.AddRange(students);
        context.SaveChanges();

        context.StudentCourses.AddRange(students.Select(s => new StudentCourse
        {
            CourseId = course.Id,
            StudentId = s.Id
        }));
        context.SaveChanges();

        var questions = BuildQuestions(course.Id, teacher.Id);
        context.Questions.AddRange(questions);
        context.SaveChanges();

        var questionTopicPairs = new (int Question, int Topic)[]
        {
            (0, 0), (1, 0), (2, 1), (3, 1), (4, 2),
            (5, 2), (6, 3), (7, 3), (8, 4), (9, 5)
        };
        context.QuestionTopics.AddRange(questionTopicPairs.Select(p => new QuestionTopic
        {
            QuestionId = questions[p.Question].Id,
            TopicId = topics[p.Topic].Id
        }));

        var questionOutcomePairs = new (int Question, int Outcome)[]
        {
            (0, 0), (1, 0), (2, 1), (3, 1), (4, 2),
            (5, 2), (6, 3), (7, 3), (8, 4), (9, 4)
        };
        context.QuestionLearningOutcomes.AddRange(questionOutcomePairs.Select(p => new QuestionLearningOutcome
        {
            QuestionId = questions[p.Question].Id,
            LearningOutcomeId = learningOutcomes[p.Outcome].Id
        }));
        context.SaveChanges();

        var exam = new Exam
        {
            CourseId = course.Id,
            Title = "Yazılım Kalite ve Test - Vize Demo Sınavı",
            ExamDate = new DateTime(2026, 4, 20),
            DurationMinutes = 60,
            ExamType = ExamType.Midterm,
            BookletCount = 1,
            ShuffleOptions = false,
            CreatedByUserId = teacher.Id
        };
        context.Exams.Add(exam);
        context.SaveChanges();

        context.ExamQuestions.AddRange(questions.Select((q, i) => new ExamQuestion
        {
            ExamId = exam.Id,
            QuestionId = q.Id,
            OrderInExam = i + 1,
            OverrideMaxPoints = 10m,
            IsCancelled = false
        }));

        var booklet = new ExamBooklet
        {
            ExamId = exam.Id,
            BookletCode = "A"
        };
        context.ExamBooklets.Add(booklet);
        context.SaveChanges();

        context.ExamBookletQuestions.AddRange(questions.Select((q, i) => new ExamBookletQuestion
        {
            BookletId = booklet.Id,
            QuestionId = q.Id,
            OrderInBooklet = i + 1
        }));
        context.SaveChanges();

        var answers = BuildAnswers(exam.Id, booklet.Id, students, questions);
        context.StudentAnswers.AddRange(answers);
        context.SaveChanges();
    }

    private static TopicLearningOutcome LinkTopicLo(Topic topic, LearningOutcome outcome) => new()
    {
        TopicId = topic.Id,
        LearningOutcomeId = outcome.Id
    };

    private static List<Question> BuildQuestions(int courseId, int teacherId)
    {
        var now = DateTime.UtcNow;
        return new List<Question>
        {
            Mc(courseId, teacherId, now, "ISO/IEC 25010 modelinde kullanılabilirlik hangi kalite karakteristiği altında değerlendirilir?", "Ürün kalitesi", "Süreç olgunluğu", "Proje maliyeti", "Kod satırı sayısı", "Ekip büyüklüğü", OptionLetter.A),
            Mc(courseId, teacherId, now, "Kalite güvencesi ile kalite kontrol arasındaki temel fark aşağıdakilerden hangisidir?", "KG önleyici süreç odaklıdır, KK ürün doğrulama odaklıdır", "KG sadece test otomasyonudur", "KK sadece dokümantasyon üretir", "KG yalnızca teslimden sonra yapılır", "KK yönetim kararıdır", OptionLetter.A),
            Mc(courseId, teacherId, now, "Bir test planında aşağıdakilerden hangisi doğrudan bulunmalıdır?", "Kapsam, riskler, kaynaklar ve kabul kriterleri", "Sadece geliştirici maaşları", "Ürün logosu", "Sunucu marka listesi", "Lisans sözleşmesi", OptionLetter.A),
            Mc(courseId, teacherId, now, "Regresyon testinin temel amacı nedir?", "Değişiklik sonrası mevcut davranışların bozulmadığını doğrulamak", "Sadece performansı artırmak", "Kod biçimlendirmek", "Veritabanını yedeklemek", "Kullanıcı eğitimi vermek", OptionLetter.A),
            Mc(courseId, teacherId, now, "Sınır değer analizi en çok hangi test tasarım tekniğiyle ilişkilidir?", "Siyah kutu test", "Beyaz kutu test", "Kod inceleme", "Sürümleme", "Statik linkleme", OptionLetter.A),
            Mc(courseId, teacherId, now, "Karar kapsamı (branch coverage) neyi ölçer?", "Karar noktalarındaki dalların çalıştırılma oranını", "Kullanıcı memnuniyetini", "Ekran sayısını", "Veritabanı boyutunu", "Derleme süresini", OptionLetter.A),
            Mc(courseId, teacherId, now, "Birim test otomasyonunda mock nesnesi hangi amaçla kullanılır?", "Bağımlılıkları izole etmek", "Veritabanını büyütmek", "UI temasını değiştirmek", "Kodun lisansını belirlemek", "Log dosyasını silmek", OptionLetter.A),
            Mc(courseId, teacherId, now, "Sürekli entegrasyon hattında otomatik testlerin temel katkısı nedir?", "Hataları erken yakalamak ve geri bildirimi hızlandırmak", "Test ihtiyacını tamamen kaldırmak", "Analiz raporlarını gizlemek", "Sadece sunumu güzelleştirmek", "Kaynak kodu kapatmak", OptionLetter.A),
            Mc(courseId, teacherId, now, "Defect leakage metriği neyi ifade eder?", "Testten kaçıp sonraki aşamada bulunan hata oranını", "Toplam test süresini", "Kod satırı sayısını", "Ekip toplantı sayısını", "Derleme sayısını", OptionLetter.A),
            Mc(courseId, teacherId, now, "Cronbach alfa değerinin yüksek olması sınav için neyi destekler?", "Maddelerin aynı yapıyı tutarlı ölçtüğünü", "Sınavın kesinlikle kolay olduğunu", "Her sorunun iptal edilmesi gerektiğini", "Çeldiricilerin hiç seçilmediğini", "Öğrenci sayısının sıfır olduğunu", OptionLetter.A)
        };
    }

    private static Question Mc(
        int courseId,
        int teacherId,
        DateTime now,
        string text,
        string a,
        string b,
        string c,
        string d,
        string e,
        OptionLetter correct) => new()
    {
        CourseId = courseId,
        QuestionText = text,
        Type = QuestionType.MultipleChoice,
        MaxPoints = 10m,
        OptionA = a,
        OptionB = b,
        OptionC = c,
        OptionD = d,
        OptionE = e,
        CorrectOption = correct,
        IsActive = true,
        IsFavorite = true,
        CreatedByUserId = teacherId,
        CreatedAt = now
    };

    private static List<StudentAnswer> BuildAnswers(int examId, int bookletId, List<Student> students, List<Question> questions)
    {
        var matrix = new[]
        {
            "AAAAAAAAAA",
            "AAAAAAAAAB",
            "AAAAAAAABA",
            "AAAAAAABAA",
            "AAAAAABAAA",
            "AAAAABAAAA",
            "AAAABAAAAB",
            "AAABAAAABB",
            "AABAAAABBB",
            "ABAAAABBBB",
            "BAAAABBBBB",
            "CAAABBBBBB",
            "DAABBBBBBB",
            "EABBBBBBBB",
            "BBBBBBBBBB",
            "BCBCBCBCBC",
            "CDEABCDBEA",
            "EEEEECCCCC"
        };

        var answers = new List<StudentAnswer>();
        for (var studentIndex = 0; studentIndex < students.Count; studentIndex++)
        {
            var pattern = matrix[studentIndex];
            for (var questionIndex = 0; questionIndex < questions.Count; questionIndex++)
            {
                var selected = ParseOption(pattern[questionIndex]);
                answers.Add(new StudentAnswer
                {
                    ExamId = examId,
                    QuestionId = questions[questionIndex].Id,
                    StudentId = students[studentIndex].Id,
                    BookletId = bookletId,
                    SelectedOption = selected,
                    IsCorrect = selected == questions[questionIndex].CorrectOption
                });
            }
        }

        return answers;
    }

    private static OptionLetter ParseOption(char value) => value switch
    {
        'A' => OptionLetter.A,
        'B' => OptionLetter.B,
        'C' => OptionLetter.C,
        'D' => OptionLetter.D,
        'E' => OptionLetter.E,
        _ => OptionLetter.Empty
    };

    private static readonly string[] DemoStudentNames =
    {
        "Ayşe Demir",
        "Mehmet Kaya",
        "Zeynep Şahin",
        "Eren Yılmaz",
        "Elif Arslan",
        "Can Aydın",
        "Merve Çelik",
        "Burak Koç",
        "İrem Yıldız",
        "Deniz Özkan",
        "Selin Polat",
        "Kerem Aslan",
        "Buse Kaplan",
        "Emre Taş",
        "Derya Kılıç",
        "Onur Ak",
        "Sude Er",
        "Ali Can"
    };
}
