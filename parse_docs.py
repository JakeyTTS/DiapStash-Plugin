import json
import re

with open(r'C:\Users\david\.gemini\antigravity\brain\686d6996-899b-475b-be31-991feba1bdbd\.system_generated\steps\6631\content.md', 'r', encoding='utf-8') as f:
    text = f.read()

match = re.search(r'<script type="application/json" data-nuxt-data="nuxt-app"[^>]*>(.*?)</script>', text)
if match:
    try:
        data = json.loads(match.group(1))
        print("Data loaded successfully.")
        # Just dump all string values to search for client id
        strings = []
        def extract_strings(obj):
            if isinstance(obj, str):
                strings.append(obj)
            elif isinstance(obj, list):
                for item in obj: extract_strings(item)
            elif isinstance(obj, dict):
                for v in obj.values(): extract_strings(v)
        extract_strings(data)
        
        for s in strings:
            if 'client_id' in s.lower() or 'secret' in s.lower() or 'diapstash' in s.lower():
                print("FOUND STRING:", s[:200])
    except Exception as e:
        print("Error parsing json:", e)

with open(r'C:\Users\david\.gemini\antigravity\brain\686d6996-899b-475b-be31-991feba1bdbd\.system_generated\steps\6633\content.md', 'r', encoding='utf-8') as f:
    text2 = f.read()
match2 = re.search(r'<script type="application/json" data-nuxt-data="nuxt-app"[^>]*>(.*?)</script>', text2)
if match2:
    try:
        data2 = json.loads(match2.group(1))
        strings2 = []
        def extract_strings2(obj):
            if isinstance(obj, str):
                strings2.append(obj)
            elif isinstance(obj, list):
                for item in obj: extract_strings2(item)
            elif isinstance(obj, dict):
                for v in obj.values(): extract_strings2(v)
        extract_strings2(data2)
        for s in strings2:
            if '#' in s or 'color' in s.lower():
                print("FOUND COLOR STRING:", s[:200])
    except Exception as e:
        print("Error parsing json2:", e)
