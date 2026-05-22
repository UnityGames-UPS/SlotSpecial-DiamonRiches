# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## CRITICAL RULES

**SCRIPTS ONLY.** Claude may only read and modify C# script files (`.cs`) in this project. Do NOT touch, open, parse, or edit any other file type — including but not limited to:

- Unity scene files (`.unity`)
- Prefabs (`.prefab`)
- ScriptableObject / asset files (`.asset`)
- Materials, shaders, textures, sprites, fonts, audio
- Meta files (`.meta`)
- Animation / animator files (`.anim`, `.controller`)
- Project settings, packages manifest, or any YAML serialized Unity data

All understanding of object relationships, component references, GameObject hierarchies, and scene structure MUST be derived solely from C# scripts. Never explore scene hierarchies or inspect serialized asset data. If a task appears to require non-script changes, stop and ask the user to make those changes in the Unity Editor.

## Project Overview

This project is a Unity client implementation of **Diamond Riches**, a slot machine game. Reference for game mechanics, paytable, symbols, and behavior: https://casino.guru/free-casino-games/slots/diamond-riches-slot-play-free

## Coding Conventions

Prefer `internal` over `public` for C# members. The project lives in a single assembly, so `internal` is sufficient for anything not requiring cross-assembly access. Reserve `public` for members that genuinely need external visibility.


## Custom Sprite Font (TMP Rich Text) — TODO: apply to other numeric text displays

The `numbers_gold` TMP sprite asset maps digits and punctuation to sprite indices:
- Digits `0`–`9` → `<sprite=0>` through `<sprite=9>`
- Full stop (`.`) → `<sprite=10>`
- Comma (`,`) → `<sprite=11>`

Use `SlotIconView.FormatWinAmount(double)` as the reference implementation when converting numeric values to this sprite-tag format. Other displays (e.g. total win, balance) may need the same treatment.

## Dependencies

- **DOTween** — all animation tweens (`DG.Tweening`)
- **Best.SocketIO** (Best HTTP) — Socket.IO client
- **Newtonsoft.Json** (Unity package) — JSON deserialization for server responses
- **TextMeshPro** — all UI text
- **Unity UI Extensions** (`com.unity.uiextensions`) — extended UI components
