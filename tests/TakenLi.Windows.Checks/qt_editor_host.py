"""Synthetic Qt fields, without Telegram login, messages or network activity."""
import json
import os
from pathlib import Path
import sys

from PySide6.QtCore import QTimer
from PySide6.QtGui import QTextCursor
from PySide6.QtWidgets import QApplication, QLineEdit, QPlainTextEdit, QVBoxLayout, QWidget

directory = Path(sys.argv[1])
scenario = json.loads((directory / "scenario.json").read_text(encoding="utf-8"))
app = QApplication([])
window = QWidget()
window.setWindowTitle("TakenLi isolated Qt editor test")
window.resize(600, 240)
layout = QVBoxLayout(window)
single = scenario["SingleLine"]
box = QLineEdit() if single else QPlainTextEdit()
text = scenario["Text"].replace("\r\n", "\n")
box.setText(text) if single else box.setPlainText(text)
layout.addWidget(box)
window.show()
window.activateWindow()
box.setFocus()
if single:
    box.setCursorPosition(scenario["Start"])
    if scenario["Length"]:
        box.setSelection(scenario["Start"], scenario["Length"])
else:
    # Test indices use Windows CRLF; Qt stores a single paragraph separator.
    start = len(scenario["Text"][:scenario["Start"]].replace("\r\n", "\n"))
    length = len(scenario["Text"][scenario["Start"]:scenario["Start"] + scenario["Length"]].replace("\r\n", "\n"))
    cursor = box.textCursor()
    cursor.setPosition(start)
    cursor.setPosition(start + length, QTextCursor.KeepAnchor)
    box.setTextCursor(cursor)


def snapshot():
    value = box.text() if single else box.toPlainText().replace("\n", "\r\n")
    start = box.selectionStart() if single else box.textCursor().selectionStart()
    length = len(box.selectedText()) if single else box.textCursor().selectionEnd() - start
    state = {"Window": int(window.winId()), "Text": value, "Start": start, "Length": length}
    temp = directory / "snapshot.tmp"
    temp.write_text(json.dumps(state), encoding="utf-8")
    os.replace(temp, directory / "snapshot.json")
    if (directory / "stop").exists():
        app.quit()


timer = QTimer()
timer.timeout.connect(snapshot)
timer.start(50)
sys.exit(app.exec())
