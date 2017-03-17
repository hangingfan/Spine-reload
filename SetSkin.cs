using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SetSkin : MonoBehaviour {

    private Dictionary<ESkin, List<string>> _skinContainAttachments = new Dictionary<ESkin, List<string>>();    //lta test
    private readonly Dictionary<ESlot, string> _dynamicSlotToAttachments = new Dictionary<ESlot, string>(); // 实际使用中的数据

    private readonly string _dynamicSkinName = "1";

    private SkeletonAnimation _skeletonAnimation;
    private Skeleton _skeleton;

    private bool _isPlayerSkinInit;  // 开关： 用于测试数据缓存 //
    void Awake()
    {
        InitSkinData();
    }

    void Start()
    {
        _skeletonAnimation = GetComponent<SkeletonAnimation>();
        _skeleton = _skeletonAnimation.skeleton;

        InitSkinDataAtStart();

        ReloadSkinByDataAtGameStart();
    }
    private void InitSkinData()
    {
        _skinContainAttachments.Add(ESkin.gun001, new List<string>() { "shooterUp_1" });
        _skinContainAttachments.Add(ESkin.gun002, new List<string>() { "2" });
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  :
    /// 1、初次游戏，记录基准 Slot -> Attachment Table
    /// 2、任何时刻实时更改套装任何部分，立刻更新映射表数据层
    /// 3、再次游戏,基于基准装，重新根据数据表缓存数据映射表现层
    /// 4、双重数据表校验，只要基准表和实际表任何部分不一致，认定装备需要Reloading
    /// ***********************************************************************
    public void InitSkinDataAtStart()
    {
        // 默认设置必须为基准装 Clothing000 //
        _skeletonAnimation.initialSkinName = _dynamicSkinName;
        ExposedList<Slot> slots = _skeleton.slots;
        for (int i = 0, n = slots.Count; i < n; i++)
        {
            Slot slot = slots.Items[i];
            String slotName = slot.data.attachmentName;
            Debug.Log("节点名称：" + slotName);
            if (slotName != null)
            {
                ESlot eSlot = MappingName2ESlot(slotName);
                Attachment attachment = LGetAttachment(i, slotName, _dynamicSkinName);
                if (attachment == null) continue;
                string attahName = attachment.Name;

                // 是否写入数据表
                SetSlotToAttachment(eSlot, attahName, false);
            }
        }
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 
    ///  在基础套装自动加载完成以后，手动调用此函数
    ///  为了局部换装数据不错乱，哪怕整套Skin换都需要更新数据表中的数据
    /// ***********************************************************************
    public void ReloadSkinByDataAtGameStart()
    {
        var slotToAttachments = DecodingAttachment();
        CompareAndSetAttachments(slotToAttachments);
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 对比数据表跟目前数据表（游戏初始加载后的Spine内置套装数据）差异，并更新数据和表现
    /// ***********************************************************************
    private void CompareAndSetAttachments(Dictionary<ESlot, string> targetAttchments)
    {
        var curAttachments = _dynamicSlotToAttachments;
        var fristSlot = (ESlot)Enum.Parse(typeof(ESlot), "Null", true);
        int start = (int)fristSlot;
        foreach (var eSlotKey in targetAttchments)
        {
            ESlot slotKey = eSlotKey.Key;
            var curAttachment = curAttachments[slotKey];
            var targetAttachment = targetAttchments[slotKey];

            if (curAttachment == null || curAttachment != targetAttachment)
            {
                ESkin eSkins = GetSkinByAttachment(targetAttachment);
                if (eSkins == ESkin.Null)
                {
                    throw new Exception("Eskin 不存在与=数据表_skinContainAttachments中");
                }
                LChangeSkinBaseOnDynamicSkin(eSkins, slotKey);
            }
        }
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 基于动态套装，改变局部并重新组合动态套装
    /// <param name="eTargetSkin">取值套装</param>
    /// <param name="eSlot">目标插槽</param>
    /// ***********************************************************************
    public bool LChangeSkinBaseOnDynamicSkin(ESkin eTargetSkin, ESlot eSlot)
    {
        Skin dynamicSkin = _skeleton.data.FindSkin(_dynamicSkinName);
        var success = LSetSkin(dynamicSkin, eTargetSkin, eSlot);
        return success;
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 
    /// 批量换装，必须保证传入的数组一一对应
    /// 批量换装只要有其中一处换不成功，整体算作失败，需要手动进行数据回滚
    /// ***********************************************************************
    public bool LChangeBitchSkinBaseOnDynamicSkin(ESkin[] eTargetSkins, ESlot[] eSlots)
    {
        if (eTargetSkins.Length != eSlots.Length) return false;
        for (int i = 0; i < eSlots.Length; i++)
        {
            var success = LChangeSkinBaseOnDynamicSkin(eTargetSkins[i], eSlots[i]);
            if (!success)
            {
                return false;  // 任意一件换不成功，整体换装失败 
            }
        }

        return true;
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 
    /// 内部处理：针对传入的需要更改的套装（实时套装），从目标皮肤中根据目标卡槽取出皮肤数据进行替换赋值操作，数据层和表现层同时处理变化
    /// <param name="dynamicSkin">赋值套装</param>
    /// <param name="eSkin">取值套装枚举</param>
    /// <param name="eSlot">目标插槽枚举</param>
    /// ***********************************************************************
    private bool LSetSkin(Skin dynamicSkin, ESkin eSkin, ESlot eSlot)
    {
        if (dynamicSkin != null)
        {
            ExposedList<Slot> slots = _skeleton.slots;
            for (int i = 0, n = slots.Count; i < n; i++)
            {
                Slot slot = slots.Items[i];
                var targetSlotName = MappingESlot2Name(eSlot);
                if (slot.data.name != targetSlotName) continue;
                string attachName = slot.data.attachmentName;
                if (attachName != null)
                {
                    string targetSkinName = MappingEskin2Name(eSkin);
                    Attachment attachment;
                    if (attachName == targetSlotName)
                    {
                        attachment = LGetAttachment(i, targetSlotName, targetSkinName);  // 重写L Get
                        dynamicSkin.Attachments.Remove(new Skin.AttachmentKeyTuple(i, targetSlotName));
                        dynamicSkin.Attachments.Add(new Skin.AttachmentKeyTuple(i, targetSlotName), attachment);
                    }
                    else
                    {
                        attachment = dynamicSkin.GetAttachment(i, attachName);   // 默认Skeleton Get
                    }

                    if (attachment != null)
                    {
                        slot.Attachment = attachment;
                        var attahName = attachment.Name;
                        SetSlotToAttachment(eSlot, attahName, true);
                        break;
                    }
                }
            }
            _skeleton.slots = slots;
        }
        _skeleton.skin = dynamicSkin;
        return true;
    }


    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 通过指定的Skin找到对应附着点的附着物Attachment
    /// ***********************************************************************
    public Attachment LGetAttachment(int slotIndex, string slotName, string skinName)
    {
        var targetSkin = _skeleton.data.FindSkin(skinName);
        var attachments = targetSkin.Attachments;
        Attachment attachment;
        attachments.TryGetValue(new Skin.AttachmentKeyTuple(slotIndex, slotName), out attachment);
        return attachment;
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 可交互对象内部绑定了对应的SkinInfo，根据SkinInfo赋值字段进行查找
    /// <param name="skinButton">换装按键</param>
    /// <param name="eSkin">对应皮肤</param>
    /// <param name="slot">对应插槽</param>
    /// ***********************************************************************
    public bool ReceiveClick(Button skinButton, ESkin eSkin, ESlot slot)
    {
        //return ResetActulSlotToAttachment(skinButton, eSkin, slot);

        return true;
    }

    private void SetSlotToAttachment(ESlot eAttach, string attchmentName, bool saveDatabase)
    {
        bool isExitKey = _dynamicSlotToAttachments.ContainsKey(eAttach);
        if (!isExitKey)
        {
            _dynamicSlotToAttachments.Add(eAttach, attchmentName);
        }
        else
        {
            _dynamicSlotToAttachments[eAttach] = attchmentName;
        }

        if (saveDatabase)
        {
            EncodingAttachment(eAttach, attchmentName);
        }
    }


    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 编码写入缓存（也可另改写为服务器存储）
    /// ***********************************************************************
    private void EncodingAttachment(ESlot eAttach, string attchmentName)
    {
        int id = (int)eAttach;
        string flag = string.Concat("slot", id.ToString());
        PlayerPrefs.SetString(flag, attchmentName);
    }

    private Dictionary<ESlot, string> DecodingAttachment()
    {
        var slotToAttachments = new Dictionary<ESlot, string>();
        var fristSlot = (ESlot)Enum.Parse(typeof(ESlot), "Null", true);
        int start = (int)fristSlot;
        int length = Enum.GetNames(typeof(ESlot)).Length;
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            string flag = string.Concat("slot", i.ToString());
            string attchmentName = PlayerPrefs.GetString(flag);
            if (attchmentName != string.Empty)
            {
                ESlot eSlot = (ESlot)i;
                slotToAttachments.Add(eSlot, attchmentName);
            }
        }
        return slotToAttachments;
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 根据插槽枚举映射对应插槽名称
    /// ***********************************************************************
    public string MappingESlot2Name(ESlot eSlot)
    {
        switch (eSlot)
        {
            case ESlot.Weapon:
                return "shooter-gun1";
            default:
                throw new ArgumentOutOfRangeException("attachment", eSlot, "换装目标不存在");
        }
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 根据插槽名称映射插槽枚举，与MappingESlot2Name( ESlot eSlot )互逆
    /// ***********************************************************************
    public ESlot MappingName2ESlot(string slotName)
    {
        if (slotName == "shooter-gun1") return ESlot.Weapon;
        return ESlot.Null;
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 根据套装贴图枚举映射贴图名称
    /// ***********************************************************************
    private string MappingEskin2Name(ESkin eSkin)
    {
        switch (eSkin)
        {
            case ESkin.gun001:
                return "gun001";
            case ESkin.gun002:
                return "gun002";
            default:
                throw new ArgumentOutOfRangeException("eSkin", eSkin, "The Skin Cannot Found in Character's Spine");
        }
    }

    /// ***********************************************************************
    /// author   : lta
    /// Created  : 03-16-2017
    /// purpose  : 通过附着物名称查找对应的套装，对_skinContainAttachments进行遍历取Key
    /// ***********************************************************************
    private ESkin GetSkinByAttachment(string attachmentName)
    {
        if (!_skinContainAttachments.Any(skins => skins.Value.Contains(attachmentName))) return ESkin.Null;

        var eSkins = _skinContainAttachments.SingleOrDefault(skin => skin.Value.Contains(attachmentName));

        return eSkins.Key;
    }
}
