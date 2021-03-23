//var base64 = require("../images/base64");
Page({
  /**
 * 页面的初始数据
 */
  data: {
    calculated: false,
    error: '',
    list_add: [],
    temp_list: [],
    showCalendar: false,
    startDate: {},
    str_StartDate: "尚未选择",
    date: "点此选择",
    company_list: [],
    selected_company: "",
    plan: [],
    selected_plan: "",
    ServerMsg: [],
    price: '0',
    CalculateText: "计算保费",
    calculateBtnEnabled: true,
    SubmitText: "提交",
    scanBtnDisabled: false
  },
  onLoad: function () {
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/miniemployeechange?openId=' + getApp().globalData.openId,
      data: {},
      header: { 'content-type': 'application/json' },
      method: 'GET',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        this.setData({
          company_list: result.data.companyNameList,
        })
        var plans = result.data.plans
        if (result.data.plans == "all") {
          plans = ["30万", "60万", "80万"],
            this.setData({
              selected_plan: plans[1],
            })
        } else {
          plans = [result.data.plans],
            this.setData({
              selected_plan: plans,
            })
        }
        this.setData({
          plan: plans,
          selected_company: this.data.company_list[0]
        })
      },
      fail: () => { },
      complete: () => { }
    });
    this.setData({
      //icon: base64.icon20,
      slideButtons: [{
        type: 'warn',
        text: '删除',
        extClass: 'test',
        //src: '/page/weui/cell/icon_del.svg', // icon的路径
      }],
    });
  },
  uncalculate: function () {
    this.setData({
      calculated: false,
      price: '0'
    })
  },
  bindCompanyChange: function (e) {
    //console.log(e)
    this.setData({
      selected_company: this.data.company_list[e.detail.value]
    })
    this.uncalculate()
  },
  bindPlanChange: function (e) {
    this.setData({
      selected_plan: this.data.plan[e.detail.value],
    })
    this.uncalculate()
  },
  bindDateChange: function (e) {
    this.setData({
      date: e.detail.value,
    })
    this.uncalculate()
  },
  selectDate: function () {
    this.setData({
      showCalendar: !this.data.showCalendar
    })
  },
  afterTapDate: function (e) {
    this.setData({
      startDate: {
        year: e.detail.year,
        month: e.detail.month,
        day: e.detail.date
      }
    })
    this.setData({
      str_StartDate: this.data.startDate.year + "年" + this.data.startDate.month + "月" + this.data.startDate.day + "日"
    })

    this.selectDate()
    console.log(this.data.str_StartDate)
  },
  changeCalculateBtnState: function (e) {
    if (this.data.CalculateText == "计算保费") {
      this.setData({
        CalculateText: "计算中...",
        calculateBtnEnabled: false,
        scanBtnDisabled: true
      })
    } else {
      this.setData({
        CalculateText: "计算保费",
        calculateBtnEnabled: true,
        scanBtnDisabled: false
      })
    }

  },
  changeSubmitBtnState: function (e) {
    if (this.data.SubmitText == "提交") {
      this.setData({
        SubmitText: "正在提交...",
        calculated: false,
        calculateBtnEnabled: false,
        scanBtnDisabled: true
      })
    } else {
      this.setData({
        SubmitText: "提交",
        calculated: true,
        calculateBtnEnabled: true,
        scanBtnDisabled: false
      })
    }
  },
  calculate: function (e) {
    var that = this
    // var debugList = [
    //   {
    //     name: '胡晓东',
    //     id: '32072119890505163X',
    //     jobtype: 'jt1',
    //     job: 'j1'
    //   }
    // ]
    // this.setData({
    //   list_add: debugList
    // })
    if (this.data.list_add.length <= 0) {
      this.showErrorMsg("人员列表为空")
      return
    }
    var dataobj = {
      personList: JSON.stringify(this.data.list_add),
      mode1: "add",
      date: this.data.date,
      company: this.data.selected_company,
      plan: this.data.selected_plan,
      openId: getApp().globalData.openId
    }
    this.changeCalculateBtnState()
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/minicalculateprice',
      data: dataobj,
      header: { 'content-type': 'application/x-www-form-urlencoded' },
      method: 'POST',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        var tempMsg = []
        if (Array.isArray(result.data)) {
          for (var i = 0; i < result.data.length; i++) {
            console.log(result.data[i])
            tempMsg.push(result.data[i])
          }
          this.setData({
            ServerMsg: tempMsg
          })
          this.popup = this.selectComponent("#popup"),
            this.popup.showPopup();
          return
        } else {
          if (result.data.indexOf("无误") == -1) {
            tempMsg.push(result.data)
            this.setData({
              ServerMsg: tempMsg
            })
            this.popup = this.selectComponent("#popup"),
              this.popup.showPopup();
            return
          }
        }
        var str_price = result.data.replace("数据无误", "")
        this.setData({
          price: str_price
        })
        this.setData({
          calculated: true
        })
        this.showErrorMsg("保费计算完成")
      },
      fail: (f) => {
        console.log(f)
      },
      complete: () => {
        this.changeCalculateBtnState()
      }
    });
  },
  success: function (e) {
    var newAdd = {
      name: e.detail.name.text,
      id: e.detail.id.text,
      jobtype: '',
      job: ''
    }
    this.data.temp_list.push(newAdd)
    this.setData({
      list_add: this.data.temp_list
    })
    this.uncalculate()
  },
  _success: function (e) {
    this.popup = this.selectComponent("#popup"),
      this.popup.hidePopup();
  },
  slideButtonTap(e) {
    var i = e.currentTarget.dataset.index;
    var t = this.data.temp_list.splice(i, 1)
    this.setData({
      list_add: this.data.temp_list
    })
    this.uncalculate()
  },
  showErrorMsg: function (e) {
    this.setData({
      error: e
    })
  },
  submit(e) {
    if (this.data.list_add.length <= 0) {
      console.log("no data")
      return
    }
    this.changeSubmitBtnState()
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/MiniUpdateSummary?date=' + this.data.date +
        '&plan=' + this.data.selected_plan +
        '&openId=' + getApp().globalData.openId,
      data: {
      },
      header: { 'content-type': 'application/json' },
      method: 'GET',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        console.log(result)
        var tempMsg = []
        if (Array.isArray(result.data)) {
          if (result.data[0] == "投保成功") {

            tempMsg.push("投保成功")
            tempMsg.push("生效日期：" + result.data[1])
            tempMsg.push("结束日期：" + result.data[2])
            tempMsg.push("本次投保人数：" + result.data[3])
            tempMsg.push("本次保费：" + result.data[4])

            this.setData({
              ServerMsg: tempMsg
            })
            this.popup = this.selectComponent("#popup")
            this.popup.showPopup();
            this.uncalculate()
            return
          }
        }
      },
      fail: (f) => {
        console.log(f)
      },
      complete: () => {
        var su = this.data.calculated
        this.changeSubmitBtnState()
        if (su == false) {
          this.setData({
            calculated: false
          })
        }
      }
    });

  }
});