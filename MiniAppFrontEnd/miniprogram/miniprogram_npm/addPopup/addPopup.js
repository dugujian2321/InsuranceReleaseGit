Component({
  options: {
    multipleSlots: true // 在组件定义时的选项中启用多slot支持
  },
  /**
   * 组件的属性列表
   */
  properties: {
    title: {            // 属性名
      type: String,     // 类型（必填），目前接受的类型包括：String, Number, Boolean, Object, Array, null（表示任意类型）
      value: '标题'     // 属性初始值（可选），如果未指定则会根据类型选择一个
    },
    // 弹窗内容
    content: {
      type: Array,

    },
    // 弹窗取消按钮文字 
    btn_no: {
      type: String,
      value: '取消'
    },
    // 弹窗确认按钮文字
    btn_ok: {
      type: String,
      value: '确定'
    },

    // 弹窗name文字
    emName: { 
      type: String,
      value: ''
    },
    // 弹窗id文字
    idNum: {
      type: String,
      value: ''
    },
    // 弹窗job文字
    job: {
      type: String,
      value: ''
    },
    // 弹窗jonType文字
    jobType: {
      type: String,
      value: '1-4'
    },
    newInfo: {
      type: Array
    },
    list_add: {
      type: Array
    }
  },

  /**
   * 组件的初始数据
   */
  data: {
    flag: true,
    scrollTop: 0,
    scanBtnDisabled: false
  },

  /**
   * 组件的方法列表
   */
  methods: {
    //隐藏弹框
    hidePopup: function () {
      this.setData({
        flag: !this.data.flag,
      })
    },
    //展示弹框
    showPopup() {
      this.setData({
        flag: !this.data.flag
      })
    },
    handleJobInput: function (e) {
      console.log(e)
      this.setData({
        job: e.detail.value
      })
    },
    handleNameInput: function (e) {
      console.log(e)
      this.setData({
        emName: e.detail.value
      })
    },
    handleIdNumInput: function (e) {
      console.log(e)
      this.setData({
        idNum: e.detail.value
      })
    },
    /*
    * 内部私有方法建议以下划线开头
    * triggerEvent 用于触发事件
    */
    _error() {
      //触发取消回调
      this.triggerEvent("error")
    },
    _success() {
      //触发成功回调
      this.setData({
        scrollTop: 0
      })
      this.triggerEvent("success");
    },
    _cancel: function (e) {
      //触发成功回调
      this.setData({
        scrollTop: 0
      })
      this.triggerEvent("cancel");
    },
    success: function (e) {
      this.setData({
        emName: e.detail.name.text,
        idNum: e.detail.id.text,
      })
      this.triggerEvent("scanSuccess")
    },
  }
})