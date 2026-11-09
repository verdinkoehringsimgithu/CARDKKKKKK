"""生成原创短促操作声音与默认主题，不改玩法配置，不启动Unity。"""
from pathlib import Path
import math
import random
import struct
import uuid
import wave

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/IslandSurvival'
THEME_GUID='a8c6d2df97af5333a3be0b24d7b42fc1'
def guid(path): return uuid.uuid5(uuid.NAMESPACE_URL,'cardkkk/island/'+path.relative_to(OUT).as_posix()).hex
def metadata(path,kind,extra=''):
    meta=Path(str(path)+'.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+guid(path)+'\n'+kind+':\n  externalObjects: {}\n'+extra+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
def main():
    audio=OUT/'Audio';audio.mkdir(exist_ok=True)
    for index,(name,seconds,frequency) in enumerate([('完成',.15,540),('拒绝',.13,190),('预警',.28,330)]):
        path=audio/(name+'.wav'); rng=random.Random(100+index)
        with wave.open(str(path),'wb') as stream:
            stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(44100)
            samples=[]
            for i in range(int(seconds*44100)):
                t=i/44100; envelope=min(1,t/.006)*math.exp(-t/(seconds*.23))
                value=.18*envelope*(math.sin(2*math.pi*frequency*t)+.2*math.sin(2*math.pi*frequency*1.51*t)+.08*(rng.random()*2-1))
                samples.append(struct.pack('<h',int(max(-1,min(1,value))*32767)))
            stream.writeframes(b''.join(samples))
        metadata(path,'AudioImporter','  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n')
    script=OUT/'Unity/SurvivalTheme.cs';metadata(script,'MonoImporter','  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n')
    theme=OUT/'Configuration/孤岛生存主题.asset'
    if not theme.exists():
        text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid(script)+', type: 3}\n  m_Name: 孤岛生存主题\n  m_EditorClassIdentifier:\n'
        for key,values in dict(Material=(.22,.30,.32),Tool=(.28,.30,.35),Facility=(.25,.32,.30),Area=(.16,.36,.33),Creature=(.29,.34,.26),Condition=(.46,.20,.22),Pulse=(.75,.60,.32),Ready=(.16,.36,.30),MissingMaterial=(.4,.3,.17),MissingFacility=(.39,.22,.23)).items(): text+='  '+key+': {r: '+str(values[0])+', g: '+str(values[1])+', b: '+str(values[2])+', a: 1}\n'
        text+='  PulseSeconds: 0.3\n'
        for key,name in [('Success','完成'),('Denied','拒绝'),('Warning','预警')]: text+='  '+key+': {fileID: 8300000, guid: '+guid(audio/(name+'.wav'))+', type: 3}\n'
        text+='  Volume: 0.25\n  Credits: "设计与制作：项目作者\\n开发协作：Codex\\n插画：项目内原创生成资产\\n声音：项目内原创合成\\n字体：Noto Sans CJK SC · SIL Open Font License"\n';theme.write_text(text,encoding='utf-8')
        Path(str(theme)+'.meta').write_text('fileFormatVersion: 2\nguid: '+THEME_GUID+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
    print('Default theme and 3 original short feedback clips written; no Unity execution.')
if __name__=='__main__':main()
