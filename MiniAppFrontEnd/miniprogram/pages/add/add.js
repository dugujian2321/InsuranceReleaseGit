//var base64 = require("../images/base64");
Page({
  /**
 * 页面的初始数据
 */
  data: {
    list_add:[]
  },
  onLoad: function () {
    this.setData({
      //icon: base64.icon20,
      slideButtons: [{
        text: '普通',
        //src: '/page/weui/cell/icon_love.svg', // icon的路径
      }, {
        text: '普通',
        extClass: 'test',
        //src: '/page/weui/cell/icon_star.svg', // icon的路径
      }, {
        type: 'warn',
        text: '警示',
        extClass: 'test',
        //src: '/page/weui/cell/icon_del.svg', // icon的路径
      }],
    });
  },
  success: function (e) {
    var k = {
      x:"1",
      y:"2"
    }
    this.data.list_add.push(k)
    
    console.log(e)
  },
  slideButtonTap(e) {
    console.log('slide button tap', e.detail)
  }
});