"""Inglizcha so'zlar ro'yxatini quradi: src/Ozbeca.Core/english.txt  (check, should, school — yozishda o'girilmaydi).

Faqat sh/ch bor so'zlar olinadi: boshqa inglizcha so'zda o'girishga narsa yo'q (tutuqli qisqartmalar — that's,
don't — CodeGuard'da qoida bilan). O'zbekchada ham uchraydigan so'z o'zbekcha deb olinadi (chop, chap, bosh,
tush, charm, hush, Toshkent, Pushkin): o'zbek korpusidagi og'irligi inglizchadagidan 1 dan ko'p kam bo'lmasa.

Manbalar (yuklab oling, repoga qo'shilmaydi):
  https://raw.githubusercontent.com/dtwinuz/bitg/main/crates/bitg-core/src/data/en.tsv
      SCOWL 2020.12.07 so'zlari va darajasi, Copyright 2000-2018 Kevin Atkinson va boshqalar (ruxsat beruvchi litsenziya)
  https://raw.githubusercontent.com/dtwinuz/bitg/main/crates/bitg-core/src/data/uz.tsv
      o'zbekcha so'zlar chastotasi, Leipzig Corpora Collection (CC BY 4.0) va boshqalar; bitg (MIT / Apache-2.0)

Ishlatish:
  python build_english.py en.tsv uz.tsv ../../src/Ozbeca.Core/english.txt
"""
import re
import sys

# O'zbekchada ham xuddi shunday yoziladi — har doim o'giriladi (korpus ko'rsatmasa ham; user qarori, 1.6.3).
UZBEK = set("chat match finish shop chop chin char flash fresh shah".split())
# SCOWL'da yo'q yoki o'zbek korpusida inglizcha nom ichida uchraydi (British Council), lekin o'zbekcha yozuv emas.
EXTRA = set("chrome cache checkout shortcut english tech british".split())


def load(path):
    out = {}
    for line in open(path, encoding="utf-8"):
        if line.startswith("#"):
            continue
        cols = line.rstrip("\n").split("\t")
        if len(cols) >= 2 and cols[1]:
            out[cols[0]] = float(cols[1])
    return out


def main(en_path, uz_path, out_path):
    en, uz = load(en_path), load(uz_path)
    words = set(EXTRA)
    for w, weight in en.items():
        if re.fullmatch(r"[a-z]+", w) and ("sh" in w or "ch" in w):
            if w in uz and uz[w] >= weight - 1.0:
                continue
            words.add(w)
    words -= UZBEK
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# tools/wordlist/build_english.py bilan yasalgan. SCOWL (c) Kevin Atkinson va boshqalar.\n")
        f.write("\n".join(sorted(words)) + "\n")
    print(len(words), "so'z")


if __name__ == "__main__":
    main(*sys.argv[1:4])
