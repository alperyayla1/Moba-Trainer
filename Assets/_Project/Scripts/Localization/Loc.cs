using System;
using System.Collections.Generic;
using System.Globalization;

namespace MobaTrainer.Localization
{
    public enum Language { English = 0, Korean = 1, Spanish = 2, PortugueseBR = 3, Turkish = 4 }

    /// <summary>
    /// Tiny string table. Keys are the English UI strings themselves, so untranslated text simply shows
    /// in English. Lookups ignore case: an all-caps source ("PRACTICE") returns the translation in caps.
    /// Use <see cref="T"/> for plain strings and <see cref="F"/> for format strings ("LAST {0} RUNS").
    /// </summary>
    public static class Loc
    {
        public static readonly string[] NativeNames = { "English", "한국어", "Español", "Português (BR)", "Türkçe" };
        private static readonly string[] CultureNames = { "en-US", "ko-KR", "es-ES", "pt-BR", "tr-TR" };

        public static Language Current { get; private set; } = Language.English;
        public static CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

        /// <summary>Raised after the language changed.</summary>
        public static event Action Changed;

        private static Dictionary<string, int> exact;
        private static Dictionary<string, int> upper;

        public static void SetLanguage(Language lang)
        {
            if ((int)lang < 0 || (int)lang >= NativeNames.Length) lang = Language.English;
            if (lang == Current && exact != null) return;
            Current = lang;
            try { Culture = CultureInfo.GetCultureInfo(CultureNames[(int)lang]); }
            catch { Culture = CultureInfo.InvariantCulture; }
            Build();
            Changed?.Invoke();
        }

        /// <summary>Best match for the OS language (first launch).</summary>
        public static Language FromSystem(UnityEngine.SystemLanguage sys)
        {
            switch (sys)
            {
                case UnityEngine.SystemLanguage.Korean: return Language.Korean;
                case UnityEngine.SystemLanguage.Spanish: return Language.Spanish;
                case UnityEngine.SystemLanguage.Portuguese: return Language.PortugueseBR;
                case UnityEngine.SystemLanguage.Turkish: return Language.Turkish;
                default: return Language.English;
            }
        }

        /// <summary>Translate an English UI string (returns it unchanged when there is no entry).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Current == Language.English) return english;
            if (exact == null) Build();
            int col = (int)Current;
            if (exact.TryGetValue(english, out int row)) return Pick(row, col, english);
            if (upper.TryGetValue(english, out row))
            {
                // Source was all-caps (menu items, headings): keep it that way in the target language.
                string tr = Pick(row, col, english);
                return tr == english ? english : tr.ToUpper(Culture);
            }
            return english;
        }

        /// <summary>Translate a format string and fill it in.</summary>
        public static string F(string englishFormat, params object[] args)
        {
            try { return string.Format(T(englishFormat), args); }
            catch (FormatException) { return string.Format(englishFormat, args); }
        }

        private static string Pick(int row, int col, string fallback)
        {
            string[] r = Table[row];
            return col < r.Length && !string.IsNullOrEmpty(r[col]) ? r[col] : fallback;
        }

        private static void Build()
        {
            exact = new Dictionary<string, int>(StringComparer.Ordinal);
            upper = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Table.Length; i++)
            {
                string en = Table[i][0];
                if (!exact.ContainsKey(en)) exact[en] = i;
                string up = en.ToUpperInvariant();
                if (up != en && !upper.ContainsKey(up)) upper[up] = i;
            }
        }

        // English, 한국어, Español, Português (BR), Türkçe
        private static readonly string[][] Table =
        {
            // ---- main menu & navigation
            new[] { "Practice", "자유 연습", "Práctica", "Prática", "Pratik" },
            new[] { "Drills", "훈련", "Ejercicios", "Exercícios", "Antrenmanlar" },
            new[] { "Play", "플레이", "Jugar", "Jogar", "Oyna" },
            new[] { "Stats", "통계", "Estadísticas", "Estatísticas", "İstatistikler" },
            new[] { "Settings", "설정", "Ajustes", "Configurações", "Ayarlar" },
            new[] { "Quit", "종료", "Salir", "Sair", "Çıkış" },
            new[] { "Resume", "계속하기", "Continuar", "Continuar", "Devam et" },
            new[] { "Restart", "다시 시작", "Reiniciar", "Reiniciar", "Yeniden başlat" },
            new[] { "Retry", "다시 하기", "Reintentar", "Tentar de novo", "Tekrar dene" },
            new[] { "Menu", "메뉴", "Menú", "Menu", "Menü" },
            new[] { "FREE PRACTICE", "자유 연습", "PRÁCTICA LIBRE", "PRÁTICA LIVRE", "SERBEST PRATİK" },
            new[] { "LATEST", "최근 기록", "RECIENTES", "RECENTES", "SON OYUNLAR" },
            new[] { "GO", "시작!", "¡YA!", "VAI!", "BAŞLA" },

            // ---- drills
            new[] { "Skillshot", "스킬샷", "Habilidad dirigida", "Skillshot", "Skillshot" },
            new[] { "Dodge", "회피", "Esquivar", "Esquiva", "Kaçış" },
            new[] { "Mixed", "혼합전", "Mixto", "Misto", "Karma" },
            new[] { "Minimap", "미니맵", "Minimapa", "Minimapa", "Mini harita" },
            new[] { "Extreme Survivor", "익스트림 서바이버", "Superviviente extremo", "Sobrevivente extremo", "Ekstrem Hayatta Kalma" },
            new[] { "Practice Tool", "연습 모드", "Herramienta de práctica", "Ferramenta de treino", "Pratik Aracı" },
            new[] { "FOR SKILLSHOT · DODGE · MIXED · MINIMAP", "스킬샷 · 회피 · 혼합전 · 미니맵에 적용", "PARA HABILIDAD · ESQUIVAR · MIXTO · MINIMAPA", "PARA SKILLSHOT · ESQUIVA · MISTO · MINIMAPA", "SKILLSHOT · KAÇIŞ · KARMA · MİNİ HARİTA İÇİN" },
            new[] { "PERFECT", "완벽", "PERFECTO", "PERFEITO", "MÜKEMMEL" },
            new[] { "PERFORMANCE", "퍼포먼스", "RENDIMIENTO", "DESEMPENHO", "PERFORMANS" },
            new[] { "SCORE", "점수", "PUNTOS", "PONTOS", "PUAN" },
            new[] { "Score", "점수", "Puntos", "Pontos", "Puan" },
            new[] { "Best", "최고", "Mejor", "Melhor", "En iyi" },
            new[] { "LEVEL {0} UNLOCKED", "레벨 {0} 해금", "NIVEL {0} DESBLOQUEADO", "NÍVEL {0} DESBLOQUEADO", "SEVİYE {0} AÇILDI" },
            new[] { "Get 75% to unlock level {0}", "75% 이상이면 레벨 {0} 해금", "Consigue 75% para desbloquear el nivel {0}", "Faça 75% para desbloquear o nível {0}", "Seviye {0} için %75 yap" },
            new[] { "Level {0} is already unlocked", "레벨 {0}은 이미 해금됨", "El nivel {0} ya está desbloqueado", "O nível {0} já está desbloqueado", "Seviye {0} zaten açık" },
            new[] { "Max level", "최고 레벨", "Nivel máximo", "Nível máximo", "En yüksek seviye" },
            new[] { "Endless run (no level progress)", "무한 모드 (레벨 진행 없음)", "Modo sin fin (sin progreso de nivel)", "Modo infinito (sem progresso de nível)", "Sonsuz mod (seviye ilerlemez)" },
            new[] { "Mouse", "마우스", "Ratón", "Mouse", "Fare" },
            new[] { "Mouse speed", "마우스 속도", "Velocidad del ratón", "Velocidade do mouse", "Fare hızı" },
            new[] { "Keep mouse inside the window", "마우스를 창 안에 고정", "Mantener el ratón en la ventana", "Manter o mouse na janela", "Fareyi pencerede tut" },
            new[] { "Auto attack", "자동 공격", "Ataque automático", "Ataque automático", "Otomatik saldırı" },
            new[] { "Attack move on key press", "키 입력 시 공격 이동", "Atacar-mover al pulsar", "Atacar-mover ao apertar", "Tuşa basınca saldırarak ilerle" },
            new[] { "Show range on attack move", "공격 이동 시 사거리 표시", "Mostrar alcance al atacar-mover", "Mostrar alcance ao atacar-mover", "Saldırarak ilerlemede menzili göster" },
            new[] { "Cast mode", "시전 방식", "Modo de lanzamiento", "Modo de conjuração", "Kullanım modu" },
            new[] { "Online", "온라인", "En línea", "Online", "Çevrimiçi" },
            new[] { "Practice with ping", "핑 연습", "Practicar con ping", "Treinar com ping", "Ping ile pratik" },
            new[] { "Free", "자유", "Libre", "Livre", "Serbest" },
            new[] { "Attack move on cursor", "커서 기준 공격 이동", "Atacar-mover al cursor", "Atacar-mover no cursor", "İmleçteki hedefe saldırarak ilerle" },
            new[] { "Attack move without click (key only)", "클릭 없이 공격 이동 (키만)", "Atacar-mover sin clic (solo tecla)", "Atacar-mover sem clique (só tecla)", "Tıklamadan saldırarak ilerle (sadece tuş)" },
            new[] { "Show attack range", "공격 사거리 표시", "Mostrar alcance de ataque", "Mostrar alcance de ataque", "Saldırı menzilini göster" },
            new[] { "Camera move speed (mouse)", "카메라 이동 속도 (마우스)", "Velocidad de cámara (ratón)", "Velocidade da câmera (mouse)", "Kamera hızı (fare)" },
            new[] { "Cast the pressed spell upon pressing another spell", "다른 스킬을 누르면 누른 스킬 시전", "Lanzar la habilidad pulsada al pulsar otra", "Lançar a habilidade pressionada ao pressionar outra", "Başka yeteneğe basınca basılı yeteneği kullan" },
            new[] { "DIFFICULTY", "난이도", "DIFICULTAD", "DIFICULDADE", "ZORLUK" },
            new[] { "LEVEL", "레벨", "NIVEL", "NÍVEL", "SEVİYE" },
            new[] { "CHOOSE AN ITEM", "아이템 선택", "ELIGE UN OBJETO", "ESCOLHA UM ITEM", "BİR EŞYA SEÇ" },
            new[] { "Items in the same group (Spellblade, Annul) can't be held together.", "같은 그룹(Spellblade, Annul)의 아이템은 함께 가질 수 없습니다.", "Los objetos del mismo grupo (Spellblade, Annul) no se pueden llevar juntos.", "Itens do mesmo grupo (Spellblade, Annul) não podem ser usados juntos.", "Aynı gruptaki (Spellblade, Annul) eşyalar birlikte taşınamaz." },
            new[] { "Random", "무작위", "Aleatorio", "Aleatório", "Rastgele" },
            new[] { "GAMBLE", "도박", "APUESTA", "APOSTA", "KUMAR" },
            new[] { "VICTORY", "승리", "VICTORIA", "VITÓRIA", "ZAFER" },
            new[] { "ENRAGED", "격노", "ENFURECIDO", "ENFURECIDO", "ÖFKELENDİ" },
            new[] { "THE EMBER COLOSSUS AWAKENS", "잿불 거신이 깨어납니다", "EL COLOSO DE ASCUAS DESPIERTA", "O COLOSSO DE BRASAS DESPERTA", "KOR DEVİ UYANIYOR" },
            new[] { "ENDLESS  (NO TIMER)", "무한 모드 (시간 제한 없음)", "SIN FIN  (SIN TIEMPO)", "INFINITO  (SEM TEMPO)", "SONSUZ  (SÜRESİZ)" },
            new[] { "ENDLESS", "무한 모드", "SIN FIN", "INFINITO", "SONSUZ" },
            new[] { "MINIMAP  ·  D = LEFT LANE  ·  F = RIGHT LANE  ·  DODGE THE RED DOTS",
                    "미니맵  ·  D = 왼쪽 라인  ·  F = 오른쪽 라인  ·  빨간 점을 피하세요",
                    "MINIMAPA  ·  D = CARRIL IZQUIERDO  ·  F = CARRIL DERECHO  ·  ESQUIVA LOS PUNTOS ROJOS",
                    "MINIMAPA  ·  D = ROTA ESQUERDA  ·  F = ROTA DIREITA  ·  DESVIE DOS PONTOS VERMELHOS",
                    "MİNİ HARİTA  ·  D = SOL KORİDOR  ·  F = SAĞ KORİDOR  ·  KIRMIZI NOKTALARDAN KAÇ" },
            new[] { "PRESS THE KEY  ·  RIGHT-CLICK THE PURPLE CIRCLES",
                    "표시된 키를 누르세요  ·  보라색 원을 우클릭하세요",
                    "PULSA LA TECLA  ·  CLIC DERECHO EN LOS CÍRCULOS MORADOS",
                    "APERTE A TECLA  ·  CLIQUE DIREITO NOS CÍRCULOS ROXOS",
                    "TUŞA BAS  ·  MOR DAİRELERE SAĞ TIKLA" },
            new[] { "RIGHT", "우클릭", "DERECHO", "DIREITO", "SAĞ" },

            // ---- results
            new[] { "NEW BEST", "신기록", "NUEVO RÉCORD", "NOVO RECORDE", "YENİ REKOR" },
            new[] { "BEST  {0}", "최고 기록  {0}", "MEJOR  {0}", "MELHOR  {0}", "EN İYİ  {0}" },
            new[] { "SURVIVED", "생존", "SOBREVIVISTE", "SOBREVIVEU", "HAYATTA KALDIN" },
            new[] { "DOWN", "사망", "CAÍDO", "ABATIDO", "ÖLDÜN" },
            new[] { "Accuracy", "명중률", "Precisión", "Precisão", "İsabet" },
            new[] { "Combos", "콤보", "Combos", "Combos", "Kombolar" },
            new[] { "Kills", "처치", "Bajas", "Abates", "Öldürme" },
            new[] { "Hit", "피격", "Golpes recibidos", "Atingido", "Yenen" },
            new[] { "Dodged", "회피함", "Esquivados", "Desviados", "Kaçılan" },
            new[] { "Dodge rate", "회피율", "Tasa de esquiva", "Taxa de esquiva", "Kaçış oranı" },
            new[] { "Time", "시간", "Tiempo", "Tempo", "Süre" },
            new[] { "Prompts", "신호", "Señales", "Sinais", "Hedefler" },
            new[] { "Reaction", "반응 속도", "Reacción", "Reação", "Tepki" },
            new[] { "{0} hit  ·  {1} missed  ·  {2} wrong", "성공 {0}  ·  놓침 {1}  ·  실수 {2}", "{0} aciertos  ·  {1} perdidos  ·  {2} errores", "{0} acertos  ·  {1} perdidos  ·  {2} erros", "{0} isabet  ·  {1} kaçan  ·  {2} hatalı" },
            new[] { "{0} dodged  ·  {1} hit", "회피 {0}  ·  피격 {1}", "{0} esquivados  ·  {1} recibidos", "{0} desviados  ·  {1} atingido", "{0} kaçıldı  ·  {1} yendi" },
            new[] { "{0}   hit {1}", "{0}   피격 {1}", "{0}   recibidos {1}", "{0}   atingido {1}", "{0}   yenen {1}" },

            // ---- stats page
            new[] { "RUNS", "플레이 수", "PARTIDAS", "PARTIDAS", "OYUN" },
            new[] { "BEST", "최고 기록", "MEJOR", "MELHOR", "EN İYİ" },
            new[] { "AVG LAST 10", "최근 10판 평균", "MEDIA ÚLT. 10", "MÉDIA ÚLT. 10", "SON 10 ORT." },
            new[] { "ACCURACY", "명중률", "PRECISIÓN", "PRECISÃO", "İSABET" },
            new[] { "DODGE RATE", "회피율", "TASA DE ESQUIVA", "TAXA DE ESQUIVA", "KAÇIŞ ORANI" },
            new[] { "HIT RATE", "성공률", "TASA DE ACIERTO", "TAXA DE ACERTO", "İSABET ORANI" },
            new[] { "LAST {0} RUNS", "최근 {0}판", "ÚLTIMAS {0} PARTIDAS", "ÚLTIMAS {0} PARTIDAS", "SON {0} OYUN" },
            new[] { "NO RUNS YET  -  PLAY THIS DRILL TO START TRACKING", "기록 없음  -  이 훈련을 플레이하면 기록이 시작됩니다", "AÚN NO HAY PARTIDAS  -  JUEGA ESTE EJERCICIO PARA EMPEZAR", "NENHUMA PARTIDA AINDA  -  JOGUE ESTE EXERCÍCIO PARA COMEÇAR", "HENÜZ OYUN YOK  -  KAYIT İÇİN BU ANTRENMANI OYNA" },
            new[] { "{0} RUNS   ·   {1} MIN TRAINED", "{0}판   ·   {1}분 훈련", "{0} PARTIDAS   ·   {1} MIN ENTRENADOS", "{0} PARTIDAS   ·   {1} MIN TREINADOS", "{0} OYUN   ·   {1} DK ANTRENMAN" },
            new[] { "acc {0}% · combos {1}", "명중 {0}% · 콤보 {1}", "prec. {0}% · combos {1}", "prec. {0}% · combos {1}", "isabet %{0} · kombo {1}" },
            new[] { "dodged {0} · hit {1}", "회피 {0} · 피격 {1}", "esquivados {0} · recibidos {1}", "desviados {0} · atingido {1}", "kaçılan {0} · yenen {1}" },
            new[] { "kills {0} · hit {1}", "처치 {0} · 피격 {1}", "bajas {0} · recibidos {1}", "abates {0} · atingido {1}", "öldürme {0} · yenen {1}" },
            new[] { "hits {0} · missed {1} · lane hits {2} · {3} ms", "성공 {0} · 놓침 {1} · 라인 피격 {2} · {3} ms", "aciertos {0} · perdidos {1} · golpes en carril {2} · {3} ms", "acertos {0} · perdidos {1} · atingido na rota {2} · {3} ms", "isabet {0} · kaçan {1} · koridorda yenen {2} · {3} ms" },
            new[] { "survived", "생존", "sobreviviste", "sobreviveu", "hayatta kaldın" },
            new[] { "down", "사망", "caído", "abatido", "öldün" },

            // ---- settings: tabs & headers
            new[] { "Audio", "오디오", "Audio", "Áudio", "Ses" },
            new[] { "Video", "비디오", "Vídeo", "Vídeo", "Görüntü" },
            new[] { "Controls", "조작", "Controles", "Controles", "Kontroller" },
            new[] { "Keybinds", "단축키", "Teclas", "Atalhos", "Tuşlar" },
            new[] { "Volume", "음량", "Volumen", "Volume", "Ses düzeyi" },
            new[] { "Output", "출력", "Salida", "Saída", "Çıktı" },
            new[] { "Display", "화면", "Pantalla", "Tela", "Ekran" },
            new[] { "Language", "언어", "Idioma", "Idioma", "Dil" },
            new[] { "Cursor", "커서", "Cursor", "Cursor", "İmleç" },
            new[] { "Camera", "카메라", "Cámara", "Câmera", "Kamera" },
            new[] { "Online feel", "온라인 환경", "Sensación online", "Sensação online", "Online his" },
            new[] { "Combat", "전투", "Combate", "Combate", "Savaş" },
            new[] { "Cast mode per key", "키별 시전 방식", "Modo de lanzamiento por tecla", "Modo de conjuração por tecla", "Tuşa göre kullanım modu" },
            new[] { "Click a key, then press the new one  -  Esc cancels", "키를 클릭한 뒤 새 키를 누르세요  -  Esc로 취소", "Haz clic en una tecla y pulsa la nueva  -  Esc cancela", "Clique numa tecla e aperte a nova  -  Esc cancela", "Bir tuşa tıkla, sonra yenisine bas  -  Esc iptal" },

            // ---- settings: rows
            new[] { "Master", "전체", "General", "Geral", "Genel" },
            new[] { "Effects", "효과음", "Efectos", "Efeitos", "Efektler" },
            new[] { "Music", "음악", "Música", "Música", "Müzik" },
            new[] { "Sound effects", "효과음 켜기", "Efectos de sonido", "Efeitos sonoros", "Ses efektleri" },
            new[] { "Lo-fi music", "로파이 음악", "Música lo-fi", "Música lo-fi", "Lo-fi müzik" },
            new[] { "Frame rate limit", "프레임 제한", "Límite de FPS", "Limite de FPS", "FPS sınırı" },
            new[] { "V-Sync", "수직 동기화", "V-Sync", "V-Sync", "V-Sync" },
            new[] { "Window mode", "창 모드", "Modo de ventana", "Modo de janela", "Pencere modu" },
            new[] { "Show FPS", "FPS 표시", "Mostrar FPS", "Mostrar FPS", "FPS göster" },
            new[] { "Mouse speed (0-100, same scale as your MOBA client)", "마우스 속도 (0-100, MOBA 클라이언트와 같은 기준)", "Velocidad del ratón (0-100, misma escala que tu cliente MOBA)", "Velocidade do mouse (0-100, mesma escala do seu cliente MOBA)", "Fare hızı (0-100, MOBA istemcinle aynı ölçek)" },
            new[] { "Fine-tune", "미세 조정", "Ajuste fino", "Ajuste fino", "İnce ayar" },
            new[] { "Cursor mode", "커서 모드", "Modo de cursor", "Modo do cursor", "İmleç modu" },
            new[] { "Lock cursor to window", "커서를 창에 고정", "Bloquear cursor en la ventana", "Prender cursor na janela", "İmleci pencereye kilitle" },
            new[] { "Apply sensitivity to system pointer", "시스템 포인터에 감도 적용", "Aplicar sensibilidad al puntero del sistema", "Aplicar sensibilidade ao ponteiro do sistema", "Hassasiyeti sistem imlecine uygula" },
            new[] { "Cursor size", "커서 크기", "Tamaño del cursor", "Tamanho do cursor", "İmleç boyutu" },
            new[] { "Camera on start", "시작 시 카메라", "Cámara al empezar", "Câmera ao iniciar", "Başlangıçta kamera" },
            new[] { "Edge pan speed", "화면 가장자리 이동 속도", "Velocidad de desplazamiento", "Velocidade de rolagem", "Kenar kaydırma hızı" },
            new[] { "Field of view", "시야각", "Campo de visión", "Campo de visão", "Görüş açısı" },
            new[] { "Simulated ping", "가상 핑", "Ping simulado", "Ping simulado", "Sanal ping" },
            new[] { "Server tick (30 Hz)", "서버 틱 (30 Hz)", "Tick del servidor (30 Hz)", "Tick do servidor (30 Hz)", "Sunucu tick'i (30 Hz)" },
            new[] { "Blink key", "점멸 키", "Tecla de destello", "Tecla de lampejo", "Işınlanma tuşu" },
            new[] { "Auto-attack when idle", "대기 중 자동 공격", "Autoataque en reposo", "Ataque automático parado", "Boştayken otomatik saldırı" },
            new[] { "Attack-move on key press (no click)", "키 입력 시 공격 이동 (클릭 없이)", "Atacar-mover al pulsar (sin clic)", "Atacar-mover ao apertar (sem clique)", "Tuşa basınca saldırarak ilerle (tıklamasız)" },
            new[] { "Queue abilities during cast time", "시전 중 스킬 예약", "Encadenar habilidades durante el lanzamiento", "Enfileirar habilidades durante a conjuração", "Kullanım sırasında yetenekleri sıraya al" },
            new[] { "Range ring on attack-move", "공격 이동 시 사거리 표시", "Anillo de alcance al atacar-mover", "Anel de alcance ao atacar-mover", "Saldırarak ilerlemede menzil halkası" },
            new[] { "Reset to defaults", "기본값으로 초기화", "Restablecer", "Restaurar padrão", "Varsayılana dön" },
            new[] { "Press a key", "키를 누르세요", "Pulsa una tecla", "Aperte uma tecla", "Bir tuşa bas" },

            // ---- settings: values
            new[] { "Unlimited", "제한 없음", "Ilimitado", "Ilimitado", "Sınırsız" },
            new[] { "Borderless", "테두리 없는 창", "Sin bordes", "Sem bordas", "Kenarlıksız" },
            new[] { "Fullscreen", "전체 화면", "Pantalla completa", "Tela cheia", "Tam ekran" },
            new[] { "Windowed", "창 모드", "Ventana", "Janela", "Pencere" },
            new[] { "Normal", "일반", "Normal", "Normal", "Normal" },
            new[] { "Quick", "빠른 시전", "Rápido", "Rápido", "Hızlı" },
            new[] { "Quick + Indicator", "빠른 시전 + 범위 표시", "Rápido + indicador", "Rápido + indicador", "Hızlı + gösterge" },
            new[] { "Gauntlet", "건틀릿", "Guantelete", "Manopla", "Eldiven" },
            new[] { "Arrow", "화살표", "Flecha", "Seta", "Ok" },
            new[] { "System", "시스템", "Sistema", "Sistema", "Sistem" },
            new[] { "Raw", "원시 입력", "Directo", "Bruto", "Ham" },
            new[] { "Unlocked", "고정 해제", "Libre", "Livre", "Serbest" },
            new[] { "Locked", "고정", "Fija", "Travada", "Kilitli" },

            // ---- actions (keybinds)
            new[] { "Move / Attack", "이동 / 공격", "Mover / Atacar", "Mover / Atacar", "Hareket / Saldırı" },
            new[] { "Select / Confirm", "선택 / 확인", "Seleccionar / Confirmar", "Selecionar / Confirmar", "Seç / Onayla" },
            new[] { "Attack Move", "공격 이동", "Atacar-mover", "Atacar-mover", "Saldırarak ilerle" },
            new[] { "Stop", "정지", "Detener", "Parar", "Dur" },
            new[] { "Hold Position", "위치 고수", "Mantener posición", "Manter posição", "Yerinde kal" },
            new[] { "Ability 1", "스킬 1", "Habilidad 1", "Habilidade 1", "Yetenek 1" },
            new[] { "Ability 2", "스킬 2", "Habilidad 2", "Habilidade 2", "Yetenek 2" },
            new[] { "Ability 3", "스킬 3", "Habilidad 3", "Habilidade 3", "Yetenek 3" },
            new[] { "Blink", "점멸", "Destello", "Lampejo", "Işınlanma" },
            new[] { "Camera Lock", "카메라 고정", "Bloquear cámara", "Travar câmera", "Kamera kilidi" },
            new[] { "Center Camera", "카메라 중앙", "Centrar cámara", "Centralizar câmera", "Kamerayı ortala" },
            new[] { "Instant Restart", "즉시 재시작", "Reinicio rápido", "Reinício rápido", "Hızlı yeniden başlat" },
            new[] { "Pause", "일시 정지", "Pausa", "Pausa", "Duraklat" },
            new[] { "Item Slot 1", "아이템 슬롯 1", "Ranura de objeto 1", "Espaço de item 1", "Eşya yuvası 1" },
            new[] { "Item Slot 2", "아이템 슬롯 2", "Ranura de objeto 2", "Espaço de item 2", "Eşya yuvası 2" },
            new[] { "Item Slot 3", "아이템 슬롯 3", "Ranura de objeto 3", "Espaço de item 3", "Eşya yuvası 3" },
            new[] { "Item Slot 4", "아이템 슬롯 4", "Ranura de objeto 4", "Espaço de item 4", "Eşya yuvası 4" },
            new[] { "Item Slot 5", "아이템 슬롯 5", "Ranura de objeto 5", "Espaço de item 5", "Eşya yuvası 5" },

            // ---- practice HUD
            new[] { "Attacks", "공격", "Ataques", "Ataques", "Saldırı" },
            new[] { "cancelled", "취소", "cancelados", "cancelados", "iptal" },
            new[] { "Attack speed", "공격 속도", "Vel. de ataque", "Vel. de ataque", "Saldırı hızı" },
            new[] { "windup", "선딜레이", "preparación", "preparação", "hazırlık" },
        };
    }
}
