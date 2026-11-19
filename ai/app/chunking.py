"""Split page text into overlapping chunks on paragraph and sentence boundaries."""
import re


def normalise(text: str) -> str:
    text = re.sub(r"[ \t\r\f\v]+", " ", text)
    text = re.sub(r"\n\s*\n+", "\n\n", text)
    return text.strip()


def chunk_text(text: str, max_chars: int = 900, overlap: int = 150) -> list[str]:
    if max_chars <= overlap:
        raise ValueError("max_chars must be greater than overlap")
    text = normalise(text)
    if not text:
        return []

    sentences = [s.strip() for s in re.split(r"(?<=[.!?])\s+|\n\n", text) if s.strip()]
    chunks: list[str] = []
    current = ""
    for sentence in sentences:
        while len(sentence) > max_chars:  # very long sentence: hard split
            head, sentence = sentence[:max_chars], sentence[max_chars - overlap:]
            if current:
                chunks.append(current)
                current = ""
            chunks.append(head)
        candidate = f"{current} {sentence}".strip()
        if len(candidate) <= max_chars:
            current = candidate
        else:
            chunks.append(current)
            tail = current[-overlap:]
            current = f"{tail[tail.find(' ') + 1:] if ' ' in tail else tail} {sentence}".strip()
    if current:
        chunks.append(current)
    return chunks
