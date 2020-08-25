const fs = require("fs").promises;
const crypto = require("crypto");
const chromium = require("chrome-aws-lambda");
const aws = require("aws-sdk");

const s3 = new aws.S3();

function md5hex(str /*: string */) {
  const md5 = crypto.createHash("md5");
  return md5.update(str, "binary").digest("hex");
}

let browser;

exports.handler = async (event, context) => {
  const html = event.html;
  const htmlMd5 = md5hex(html);
  const now = Date.now();
  const bucketName = process.env.BucketName;
  const efsMountPath = process.env.EfsMountPath;
  const objectKey = now + "-" + htmlMd5 + ".png";
  const efsPutDirPath = efsMountPath + "/screenshots";
  const efsPutFilePath = efsPutDirPath + "/" + objectKey;
  console.log(html);
  console.log(htmlMd5);
  console.log(bucketName);
  console.log(efsMountPath);
  console.log(objectKey);
  console.log(efsPutDirPath);
  console.log(efsPutFilePath);

  // 立ち上げに時間がかかる（1024MBメモリで3秒ほど）ので実行毎ではなく1回立ち上げる。
  if (!browser) {
    console.log("Start launch browser.");
    browser = await chromium.puppeteer.launch({
      args: chromium.args,
      defaultViewport: chromium.defaultViewport,
      executablePath: await chromium.executablePath,
      headless: chromium.headless,
    });
    console.log("End launch browser.");
  }

  console.log("Start browse.");
  let page = await browser.newPage();
  await page.setContent(html);
  console.log("End browse.");

  console.log("Start screenshot.");
  // S3向け
  const screenshot = await page.screenshot({
    type: "png",
    fullPage: true,
    omitBackground: true,
    encoding: "binary",
  });
  // EFSへの保存
  try {
    await fs.mkdir(efsPutDirPath);
  } catch (err) {
    console.log(err);
  }
  await page.screenshot({
    type: "png",
    fullPage: true,
    omitBackground: true,
    path: efsPutFilePath,
  });
  console.log("End screenshot.");

  page.close();

  // 毎回立ち上げるならbrowserも掃除する
  // await browser.close();

  console.log("Start put S3.");
  const uploadResponse = await s3
    .upload({
      Bucket: bucketName,
      Key: objectKey,
      Body: screenshot,
      ContentType: "image/png",
    })
    .promise()
    .then((res) => res)
    .catch((e) => Promise.reject(e));
  console.log("End put S3.");
  console.log(uploadResponse);

  return {
    location: uploadResponse.Location,
  };
};
