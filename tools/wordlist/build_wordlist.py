"""Tutuqsiz yozilgan o'/g' so'zlar lug'atini quradi: src/Ozbeca.Core/words.txt  (ozbek → o'zbek).

Faqat BIR MA'NOLI so'zlar olinadi — tutuqsiz shakli boshqa haqiqiy so'z bo'lsa (ot/o't, qol/qo'l, qoshiq/qo'shiq),
u ro'yxatga kirmaydi. Uch tekshiruv:
  1. Korpus (Leipzig): "toza" gaplarda (tutuqni to'g'ri yozadigan) so'z tutuqsiz uchrasa — u haqiqiy so'z.
     O'zak oilasi ham tekshiriladi: qol → qoldi, qolgan (toza gaplarda ko'p) — demak qol ikki ma'noli.
  2. Inglizcha eng ko'p ishlatiladigan 10 000 so'z bilan to'qnashuv (bug, song, yoga).
  3. Hunspell uz_UZ.dic: tutuqsiz lemma bo'lsa (qoshiq) yoki lemma + mos turkumdagi qo'shimcha (ot/X + di).
     Hunspell faqat chiqarib tashlash uchun ishlatiladi — undan ro'yxatga hech narsa qo'shilmaydi.
Qo'shimchalar o'zbek grammatikasidagi tartib bilan tekshiriladi (ko'plik → egalik → kelishik → yuklama),
aks holda "or + tasida" kabi tasodifiy bo'linishlar o'rtasida'ni ham tashlab yuborardi.

Manbalar (yuklab oling, repoga qo'shilmaydi):
  https://downloads.wortschatz-leipzig.de/corpora/uzb_community_2017.tar.gz         (CC BY 4.0)
  https://downloads.wortschatz-leipzig.de/corpora/uzb_wikipedia_2021_100K.tar.gz    (CC BY 4.0)
  https://raw.githubusercontent.com/first20hours/google-10000-english/master/google-10000-english-no-swears.txt
  https://raw.githubusercontent.com/uzbek-net/uz-hunspell/master/uz_UZ.dic
  https://raw.githubusercontent.com/dtwinuz/bitg/main/crates/bitg-core/src/data/uz.tsv   (--lexicon, ixtiyoriy)
      kirill matndan olingan so'z chastotasi (Leipzig CC BY 4.0, Common Voice CC0, Tatoeba CC BY 2.0 FR; bitg — MIT /
      Apache-2.0). Kirillda ў/ғ alohida harf — tutuqsiz shakl haqiqiy so'zmi, shu aytadi. Lotin korpusida kam uchragan
      qo'shimchali shakllarni beradi (bo'lishi, o'rtasida): o'zak + qo'shimcha tekshiruvi ham kirill chastotasi bilan.

Ishlatish:
  python build_wordlist.py OUT.txt EN10K.txt uz_UZ.dic SENTENCES1.txt [SENTENCES2.txt ...] [--lexicon uz.tsv]
"""
import bisect
import collections
import functools
import re
import sys

MIN_FULL = 3      # tutuqli shakl korpusda kamida shuncha marta
RATIO = 0.03      # toza tutuqsiz / tutuqli — bundan ko'p bo'lsa ikki ma'noli
ABS = 6           # bundan kam toza uchrash — tasodifiy xato deb olinadi
STEM_MIN = 30     # o'zak alohida so'z sifatida (tutuqli) kamida shuncha uchrasa bo'linish tekshiriladi
LEX_MIN = 1.5     # --lexicon: kirill korpusida kamida ~3 marta (og'irlik log10(chastota)+1)
LEX_STEM_GAP = 1.5  # tutuqsiz o'zak tutuqlisidan ~30 marta siyrak bo'lsa — o'zbekchada deyarli uchramaydi
LEX_STEM_MIN = 2.0  # va kirillda 10 martadan kam uchragan tutuqsiz o'zak (bol, ism, qisqartma) — shovqin

APOS = "'`’‘ʻʼ´′＇‛"
TOK = re.compile(r"[A-Za-z" + re.escape(APOS) + r"]+")
CYR = re.compile(r"[Ѐ-ӿ]")
# Gapni "shalaq" deb belgilaydigan aniq xato shakllar (bunday gaplar toza hisoblanmaydi).
SLOPPY = set("bolgan bolib boladi boyicha ozbekiston ozbek kop togri bolsa boldi korinadi bolishi kopchilik "
             "korsatadi bolmagan".split())
# Tutuqsiz shakli inglizcha so'z, lekin top-10k ro'yxatida yo'q: yozish paytida buzilmasin (logon -> log'on).
# tog'a — haqiqiy o'zbekcha so'z, qoladi (inglizcha toga o'giriladi). Taklif: XakimovNosirbek (PR #1).
ENGLISH_HOMOGRAPHS = {"logon"}

# --- Qo'shimchalar grammatikasi (soddalashtirilgan, tartib bilan) ---
N_DERIV = {"li", "lik", "siz", "chi", "chilik", "cha"}
N_PLUR = {"lar"}
N_POSS = {"im", "ing", "i", "si", "imiz", "ingiz", "miz", "ngiz", "m", "ng", "lari"}
N_CASE = {"ning", "ni", "ga", "ka", "qa", "da", "dan", "gacha"}
N_PART = {"dagi", "gi", "dek", "day", "gina", "mi", "dir", "ku"}
NOUN_SLOTS = [N_DERIV, N_PLUR, N_POSS, N_CASE, N_PART]

V_VOICE = {"dir", "tir", "il", "in", "ish"}
V_NEG = {"ma"}
V_CORE = {"gan", "kan", "di", "adi", "ydi", "ib", "moq", "ish", "sa", "ar", "yap", "yapti", "apti", "ayotgan",
          "yotgan", "guncha", "gach", "kach", "digan", "adigan", "ydigan", "moqchi", "may", "mas", "masdan",
          "moqda", "gin", "sin", "inglar", "aylik", "dim", "ding", "dik", "dingiz", "dilar", "aman", "asan",
          "amiz", "asiz", "uvchi", "ganlik"}
V_PERSON = {"man", "san", "miz", "siz", "lar", "k"}


@functools.lru_cache(maxsize=None)
def noun_chain(s, slot=0):
    """s — ot qo'shimchalari zanjirimi (slot'dan boshlab, tartib bilan)."""
    if s == "":
        return True
    for i in range(slot, len(NOUN_SLOTS)):
        for x in NOUN_SLOTS[i]:
            if s.startswith(x) and noun_chain(s[len(x):], i + 1):
                return True
    return False


@functools.lru_cache(maxsize=None)
def verb_chain(s):
    """[nisbat] [ma] asosiy_shakl [shaxs | ot qo'shimchalari (sifatdosh: o'tganidan)]."""
    for v in [""] + sorted(V_VOICE):
        if not s.startswith(v):
            continue
        r = s[len(v):]
        for n in ("", "ma"):
            if not r.startswith(n):
                continue
            r2 = r[len(n):]
            for c in V_CORE:
                if r2.startswith(c):
                    tail = r2[len(c):]
                    if tail == "" or tail in V_PERSON or noun_chain(tail, 2):
                        return True
    return False


def classes(s):
    """Bo'sh bo'lmagan s qaysi turkum qo'shimchasi bo'la oladi: {'N','V'}."""
    out = set()
    if noun_chain(s):
        out.add("N")
    if verb_chain(s):
        out.add("V")
    return out


VOWELS = set("aeiou")
# Yozishmalarda ko'p uchraydigan qisqargan shakllar (bo'lmoq, to'g'ri). Kengaytirish: user so'zni aytsa — shu yerga.
COLLOQUIAL = ["to'g'rimas", "to'rimas", "to'g'rimasmi", "to'rimasmi", "bo'pti", "bo'ptimi", "bo'ptiku", "bo'pqoldi",
              "bo'pqopti", "bo'mapti", "bo'mabdi", "bo'votti", "bo'vottimi", "bo'masa", "bo'mas", "bo'maydi",
              "bo'maydimi", "bo'mayapti", "bo'vurdi", "bo'tti", "bo'ttimi", "bo'ttiku"]
# Unli bilan tugaydigan fe'l o'zagi qo'shimchalari (to'g'irla + sh, to'g'irla + shimiz) — so'zlashuv o'zaklari uchun.
STEM_ENDINGS = ["", "b", "bman", "bsan", "bdi", "bdimi", "di", "dim", "ding", "dik", "dingiz", "dilar", "dimi",
                "dingmi", "sa", "sam", "sang", "sak", "sangiz", "salar", "sh", "shim", "shing", "shi", "shimiz", "shingiz",
                "shga", "shni", "shdan", "shda", "shadi", "shamiz", "shasiz", "shyapti", "shib", "shgan", "shmoqchi",
                "y", "yman", "ysan", "ydi", "ymiz", "ysiz", "ydilar", "ymi", "ylik", "yapman", "yapti", "yapmiz",
                "yapsiz", "yaptimi", "ng", "nglar", "ngchi", "gan", "ganman", "gani", "ganmiz", "sin", "sinlar",
                "moq", "moqchi", "moqchiman", "vchi", "vdi", "vdim", "vurdi", "volsa", "volaman", "ttim", "tti"]
COLLOQUIAL_STEMS = ["to'g'irla", "to'g'rila"]
# "o'z" olmoshi: "oz" (kam, ozmoq) bilan ikki ma'noli, lekin yozishmada deyarli doim o'zi/o'zim. Tekshiruvsiz qo'shiladi —
# user qarori (2026-11-12). Narxi: kam uchraydigan "ozi qoldi", "ozim" (ekin) ham o'giriladi (Backspace qaytaradi).
OZ_PRONOUN = ["o'z" + p + c for p in ("i", "im", "ing", "imiz", "ingiz", "lari")
              for c in ("", "ni", "ga", "da", "dan", "ning")]


def colloquial(forms):
    """Lug'atdagi shakllardan so'zlashuv shakllari: bo'lmadi → bo'madi, bo'lib → bo'p, to'g'rimi → to'rimi,
    to'g'rilash → to'g'irlash; yana COLLOQUIAL va COLLOQUIAL_STEMS + STEM_ENDINGS.
    Har biri keyin ambiguous() dan o'tadi: bo'ldi → bo'di (bodi), to'g'risi → to'risi (tor+i) tushib qoladi."""
    out = set(COLLOQUIAL) | {s + e for s in COLLOQUIAL_STEMS for e in STEM_ENDINGS}
    for f in forms:
        if f.startswith("bo'lib"):
            out.add("bo'p" + f[6:])
        elif f.startswith("bo'l") and not f.startswith("bo'lmach") and len(f) > 4 and f[4] not in VOWELS and f[4] != "'":
            out.add("bo'" + f[4:])
        if f.startswith("to'g'ri") and not f.startswith("to'g'ril"):
            out.add("to'ri" + f[7:])
        if f.startswith("to'g'rila"):
            out.add("to'g'irla" + f[9:])
    return out


def norm(w):
    w = w.lower()
    for a in APOS:
        w = w.replace(a, "'")
    w = w.lstrip("'")
    return w if w.endswith("g'") else w.rstrip("'")   # bog', tog' — tutuq; boshqa oxirgi ' — tirnoq


def key(w):
    return re.sub(r"(?<=[og])'", "", w)


def last_mark(f):
    """Tutuqli shakldagi oxirgi o'/g' harfining kalitdagi indeksi."""
    j, last = 0, 0
    for ch in f:
        if ch == "'":
            last = j - 1
        else:
            j += 1
    return last


def read_corpus(paths):
    full = collections.Counter()
    forms = collections.defaultdict(collections.Counter)
    clean = collections.Counter()
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for line in f:
                text = line.split("\t", 1)[-1]
                if CYR.search(text):
                    continue
                good, plain, sloppy = 0, [], False
                for t in map(norm, TOK.findall(text)):
                    if not t:
                        continue
                    k = key(t)
                    if k != t:
                        if "'" in k:          # boshqa tutuq ham bor (ma'no) — o'tkazamiz
                            continue
                        full[k] += 1
                        forms[k][t] += 1
                        good += 1
                    elif "'" not in t:
                        plain.append(t)
                        sloppy |= t in SLOPPY
                if good >= 2 and not sloppy:
                    clean.update(plain)
    return full, {k: c.most_common(1)[0][0] for k, c in forms.items()}, clean


def read_lexicon(path):
    """bitg uz.tsv: kirill matndan olingan eski lotin so'z → log10(chastota)+1 (0 — kirillda uchramaydi).
    Kirillda ў/ғ alohida harf, shuning uchun tutuqsiz shakl (ot, qol) kirillda uchrasa — u haqiqiy so'z."""
    lex = {}
    for line in open(path, encoding="utf-8"):
        if line.startswith("#"):
            continue
        cols = line.rstrip("\n").split("\t")
        if len(cols) >= 2 and cols[1]:
            lex[cols[0]] = float(cols[1])
    return lex


def main():
    args = [a for a in sys.argv[1:] if a != "-v"]
    lex = {}
    if "--lexicon" in args:
        i = args.index("--lexicon")
        lex = read_lexicon(args[i + 1])
        del args[i:i + 2]
    out_path, en_path, dic_path, *sentences = args
    full, form, clean = read_corpus(sentences)
    en = {w.strip() for w in open(en_path, encoding="utf-8") if w.strip()}

    verbs, nouns = set(), set()
    for line in open(dic_path, encoding="utf-8"):
        word, _, flags = line.strip().partition("/")
        w = norm(word)
        if w and "'" not in w and re.fullmatch(r"[a-z]+", w):
            (verbs if "X" in flags else nouns).add(w)
    lemmas = verbs | nouns

    clean_sorted, full_sorted = sorted(clean), sorted(full)

    def family(p, words, sk, cls):
        i, total = bisect.bisect_left(sk, p), 0
        while i < len(sk) and sk[i].startswith(p):
            rest = sk[i][len(p):]
            if (rest == "" and len(cls) == 2) or (rest and classes(rest) & cls):
                total += words[sk[i]]
            i += 1
        return total

    @functools.lru_cache(maxsize=None)
    def corpus_ambiguous(p, cls):
        pf = family(p, clean, clean_sorted, cls)
        return pf > ABS and pf > RATIO * family(p, full, full_sorted, cls)

    def ambiguous(k, f, n, stems=True):
        """Tutuqsiz kalit boshqa haqiqiy so'z bo'la oladimi — sababi yoki None.
        stems=False: o'zak + qo'shimcha bo'linishi tekshirilmaydi (kirill korpusi buni o'zi aytadi)."""
        if k in en or k in ENGLISH_HOMOGRAPHS:
            return "inglizcha"
        if k in lemmas:
            return "hunspell lemma"
        if clean[k] > ABS and clean[k] > RATIO * n:
            return "korpus: o'zi"
        if not stems:
            return None
        for cut in range(max(2, last_mark(f) + 1), len(k) + 1):
            p, s = k[:cut], k[cut:]
            cls = classes(s) if s else {"N", "V"}
            if not cls:
                continue
            if s and ((p in verbs and "V" in cls) or (p in nouns and "N" in cls)):
                return "hunspell o'zak"
            if (not s or full[p] >= STEM_MIN) and corpus_ambiguous(p, frozenset(cls)):
                return "korpus: o'zak"
        return None

    def lex_stem(k, f):
        """Kirill korpusi bo'yicha o'zak tekshiruvi: boshliq + dan (bo'shliqdan emas), och + maydi (o'chmaydi emas).
        Tutuqsiz o'zak kirillda o'z holicha uchrasa va tutuqli o'zakdan LEX_STEM_GAP dan kam siyrak bo'lsa — ikki ma'noli.
        bol + ishi — bol kirillda kam (1.88), shuning uchun bo'lishi olinadi; ot + dim (3.04 / o't 4.09) — olinmaydi."""
        stem_full, j = "", 0
        for cut in range(1, len(k)):
            while len(key(stem_full)) < cut:
                stem_full += f[j]
                j += 1
            while j < len(f) and f[j] == "'":
                stem_full += f[j]
                j += 1
            if cut <= last_mark(f) or not classes(k[cut:]):
                continue
            bare = lex.get(k[:cut], 0)
            if bare >= LEX_STEM_MIN and bare >= lex.get(stem_full, 0) - LEX_STEM_GAP:
                return "kirill: o'zak"
        return None

    result, stats = [], collections.Counter()
    for k, n in full.items():
        f = form[k]
        if n < MIN_FULL or len(k) < 3 or not re.fullmatch(r"[a-z']+", f):
            continue
        why = ambiguous(k, f, n)
        if why:
            stats[why] += 1
            continue
        result.append(f)

    # Kirill korpusidagi shakllar (bitg uz.tsv): lotin korpusida kam uchragan qo'shimchali so'zlar (bo'lishi, o'rtasida).
    # Tutuqsiz shakli kirillda ham uchrasa — ikki ma'noli, olinmaydi; qolganlari yuqoridagi tekshiruvlardan o'tadi.
    keys = {key(f) for f in result}
    by_key = collections.defaultdict(list)   # yog'i va yo'g'i — kaliti bir xil (yogi), qaysi biri ekani noma'lum
    for f, weight in lex.items():
        if weight > 0:
            by_key[key(f)].append(weight)
    for f, weight in lex.items():
        k = key(f)
        if k == f or k in keys or "'" in k or len(k) < 3 or weight < LEX_MIN or not re.fullmatch(r"[a-z']+", f):
            continue
        if lex.get(k, 0) > 0:
            stats["kirill: tutuqsizi ham bor"] += 1
            continue
        if any(w != weight and w >= weight - LEX_STEM_GAP for w in by_key[k]) or by_key[k].count(weight) > 1:
            stats["kirill: bir nechta shakl"] += 1
            continue
        why = ambiguous(k, f, max(MIN_FULL, round(10 ** (weight - 1))), stems=False) or lex_stem(k, f)
        if why:
            stats["kirill: " + why] += 1
            continue
        keys.add(k)
        result.append(f)

    # So'zlashuv shakllari (bo'madi, to'rimas, to'g'irlash) — korpusda deyarli yo'q, qoida bilan yasaladi va
    # xuddi shu tekshiruvlardan o'tadi. n = MIN_FULL: korpusda tutuqsiz uchrasa ham ikki ma'noli deb olinadi.
    keys = {key(f) for f in result}
    added = []
    for f in sorted(colloquial(result)):
        k = key(f)
        if k in keys or len(k) < 3:
            continue
        why = ambiguous(k, f, MIN_FULL)
        if why:
            stats["so'zlashuv: " + why] += 1
            if "-v" in sys.argv:
                print("  rad:", f, "-", why)
            continue
        keys.add(k)
        added.append(f)
    added += [f for f in OZ_PRONOUN if key(f) not in keys]
    result += added

    result.sort()
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Özbeça — tutuqsiz yozilgan o'/g' so'zlar (bir ma'noli). tools/wordlist/build_wordlist.py bilan yasalgan.\n")
        f.write("# Manba: Leipzig Corpora Collection (uzb_community_2017, uzb_wikipedia_2021_100K), CC BY 4.0.\n")
        if lex:
            f.write("# Kirill korpusi chastotasi: bitg uz.tsv (Leipzig CC BY 4.0, Common Voice CC0, Tatoeba CC BY 2.0 FR).\n")
        f.write("\n".join(result) + "\n")
    print(len(result), "so'z (shundan so'zlashuv:", len(added), ");", "chiqarildi:", dict(stats))
    if "-v" in sys.argv:
        print("so'zlashuv:", " ".join(added))


if __name__ == "__main__":
    main()
